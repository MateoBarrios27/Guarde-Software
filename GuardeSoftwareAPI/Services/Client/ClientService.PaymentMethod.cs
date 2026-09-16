using System.Data;
using System.Globalization;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Utils;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.client;

public partial class ClientService
{
    public async Task<PaymentMethodChangeContextDto> GetPaymentMethodChangeContextAsync(int clientId)
    {
        using var connection = accessDB.GetConnectionClose();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var (_, context) = await ReadPaymentMethodContextAsync(clientId, connection, transaction);
        await transaction.CommitAsync();
        return context;
    }

    private async Task<(int RentalId, PaymentMethodChangeContextDto Context)> ReadPaymentMethodContextAsync(
        int clientId, SqlConnection connection, SqlTransaction transaction)
    {
        const string sql = @"
            SELECT r.rental_id, ISNULL(c.preferred_payment_method_id, 0),
                   ISNULL(pm.name, 'Sin método'), ISNULL(pm.commission, 0), h.amount, r.increase_anchor_date
            FROM clients c WITH (UPDLOCK, HOLDLOCK)
            CROSS APPLY (SELECT TOP 1 rental_id, increase_anchor_date FROM rentals WITH (UPDLOCK, HOLDLOCK)
                         WHERE client_id = c.client_id AND active = 1 ORDER BY rental_id DESC) r
            CROSS APPLY (SELECT TOP 1 amount FROM rental_amount_history WITH (UPDLOCK, HOLDLOCK)
                         WHERE rental_id = r.rental_id AND start_date <= @today
                         ORDER BY start_date DESC, rental_amount_history_id DESC) h
            LEFT JOIN payment_methods pm ON pm.payment_method_id = c.preferred_payment_method_id
            WHERE c.client_id = @id";
        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id", clientId);
        command.Parameters.AddWithValue("@today", TimeHelper.GetArgentinaTime().Date);
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("El cliente necesita un alquiler activo y un abono vigente.");
        return (reader.GetInt32(0), new PaymentMethodChangeContextDto
        {
            PaymentMethodId = reader.GetInt32(1), PaymentMethodName = reader.GetString(2),
            Commission = reader.GetDecimal(3), Amount = reader.GetDecimal(4),
            NextIncreaseDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
        });
    }

    public async Task<string> ChangePaymentMethodAsync(int clientId, ChangePaymentMethodDto request)
    {
        if (request.PaymentMethodId <= 0 || request.Amount < 0 || request.Amount > 99999999.99m
            || decimal.Round(request.Amount, 2) != request.Amount)
            throw new ArgumentException("Seleccioná un método y un abono válido con hasta dos decimales.");

        using var connection = accessDB.GetConnectionClose();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var (rentalId, current) = await ReadPaymentMethodContextAsync(clientId, connection, transaction);
            if (current.PaymentMethodId != request.ExpectedPaymentMethodId || current.Amount != request.ExpectedAmount)
                throw new InvalidOperationException("El método o abono cambió desde que abriste el modal. Volvé a abrirlo.");
            if (current.PaymentMethodId == request.PaymentMethodId)
                throw new ArgumentException("Elegí un método de pago diferente al actual.");

            string newMethodName;
            using (var method = new SqlCommand("SELECT name FROM payment_methods WHERE payment_method_id = @id AND active = 1", connection, transaction))
            {
                method.Parameters.AddWithValue("@id", request.PaymentMethodId);
                newMethodName = (await method.ExecuteScalarAsync()) as string
                    ?? throw new ArgumentException("El método de pago ya no está disponible.");
            }

            // Rebuild from the ledger under the same transaction before checking payment coverage.
            await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rentalId, connection, transaction);
            string? adjustedMonth = null;
            string result = "No hay un débito mensual generado para ajustar.";
            DateTime? lastMonth = null;
            decimal debit = 0, paidTowardsMonth = 0, outstanding = 0;
            using (var balance = new SqlCommand(@"
                SELECT TOP 1 month_year, monthly_debits,
                       ISNULL(monthly_debits, 0) - ISNULL(unpaid_rent, 0),
                       balance - paid - advanced_payment
                FROM client_month_balances WHERE rental_id = @id
                ORDER BY RIGHT(month_year, 4) DESC, LEFT(month_year, 2) DESC", connection, transaction))
            {
                balance.Parameters.AddWithValue("@id", rentalId);
                using var reader = await balance.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    lastMonth = DateTime.ParseExact(reader.GetString(0), "MM/yyyy", CultureInfo.InvariantCulture);
                    debit = reader.GetDecimal(1);
                    paidTowardsMonth = Math.Max(0, reader.GetDecimal(2));
                    outstanding = reader.GetDecimal(3);
                }
            }

            if (lastMonth.HasValue)
            {
                result = "Se conservó el débito del último mes: ya tiene al menos el 60 % cubierto o está saldado.";
                if (debit > 0 && outstanding > 0 && paidTowardsMonth < debit * 0.60m)
                {
                    var rentMovements = new List<int>();
                    using (var movements = new SqlCommand(@"
                        SELECT movement_id, movement_date, concept FROM account_movements
                        WHERE rental_id = @id AND movement_type = 'DEBITO' AND concept LIKE 'Alquiler %'
                          AND payment_id IS NULL", connection, transaction))
                    {
                        movements.Parameters.AddWithValue("@id", rentalId);
                        using var reader = await movements.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            var movement = new AccountMovement { Id = reader.GetInt32(0), MovementDate = reader.GetDateTime(1), Concept = reader.GetString(2) };
                            if (ClientMonthBalanceService.ResolveMonthStart(movement) == lastMonth.Value)
                                rentMovements.Add(movement.Id);
                        }
                    }
                    if (rentMovements.Count > 1)
                        throw new InvalidOperationException("El último mes tiene varios débitos de alquiler. Revisalos antes de cambiar el método.");
                    if (rentMovements.Count == 1)
                    {
                        using var update = new SqlCommand("UPDATE account_movements SET amount = @amount WHERE movement_id = @id", connection, transaction);
                        update.Parameters.AddWithValue("@amount", request.Amount);
                        update.Parameters.AddWithValue("@id", rentMovements[0]);
                        await update.ExecuteNonQueryAsync();
                        adjustedMonth = lastMonth.Value.ToString("MM/yyyy");
                        result = $"Se actualizó el débito de alquiler de {adjustedMonth}.";
                    }
                    else result = "No se encontró un débito de alquiler ajustable en el último mes generado.";
                }
            }

            var now = TimeHelper.GetArgentinaTime();
            await rentalAmountHistoryService.UpsertRentalAmountHistoryTransactionAsync(rentalId, request.Amount, now.Date, connection, transaction);
            using (var save = new SqlCommand(@"
                UPDATE clients SET preferred_payment_method_id = @method WHERE client_id = @client;
                INSERT INTO client_payment_method_changes
                    (rental_id, changed_at, old_method_name, new_method_name, old_amount, new_amount, adjusted_month)
                VALUES (@rental, @now, @oldName, @newName, @oldAmount, @amount, @month);", connection, transaction))
            {
                save.Parameters.AddWithValue("@method", request.PaymentMethodId);
                save.Parameters.AddWithValue("@client", clientId);
                save.Parameters.AddWithValue("@rental", rentalId);
                save.Parameters.AddWithValue("@now", now);
                save.Parameters.AddWithValue("@oldName", current.PaymentMethodName);
                save.Parameters.AddWithValue("@newName", newMethodName);
                save.Parameters.AddWithValue("@oldAmount", current.Amount);
                save.Parameters.AddWithValue("@amount", request.Amount);
                save.Parameters.AddWithValue("@month", (object?)adjustedMonth ?? DBNull.Value);
                await save.ExecuteNonQueryAsync();
            }
            await _clientMonthBalanceService.RebuildForRentalTransactionAsync(rentalId, connection, transaction);
            await transaction.CommitAsync();
            return result;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }
}
