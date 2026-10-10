using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Client;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.clientMonthBalance;
using GuardeSoftwareAPI.Services.payment;
using GuardeSoftwareAPI.Utils;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.client;

public partial class ClientService
{
    private sealed record ReactivationBalanceData(int? RentalId, decimal NetDebt,
        decimal RentDebt, decimal InterestDebt, decimal Credit, decimal PendingSurcharge, DateTime SettlementDate);

    public async Task<ClientReactivationContextDto> GetReactivationContextAsync(int clientId)
    {
        using var connection = accessDB.GetConnectionClose();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var data = await ReadReactivationBalanceAsync(clientId, connection, transaction);
        await transaction.CommitAsync();
        return new() { RentalId = data.RentalId, Balance = -data.NetDebt };
    }

    private async Task<ReactivationBalanceData> ReadReactivationBalanceAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
    {
        const string query = @"
            SELECT c.active, r.rental_id, ISNULL(r.pending_surcharge, 0), r.active
            FROM clients c WITH (UPDLOCK, HOLDLOCK)
            OUTER APPLY (
                SELECT TOP 1 rental_id, pending_surcharge, active FROM rentals WITH (UPDLOCK, HOLDLOCK)
                WHERE client_id = c.client_id ORDER BY active DESC, start_date DESC, rental_id DESC
            ) r
            WHERE c.client_id = @client_id AND ISNULL(c.is_deleted, 0) = 0;";
        int? rentalId;
        decimal pending;
        using (var command = new SqlCommand(query, connection, transaction))
        {
            command.Parameters.Add(new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId });
            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new KeyNotFoundException("No se encontró el cliente.");
            if (reader.GetBoolean(0) || (!reader.IsDBNull(3) && reader.GetBoolean(3)))
                throw new InvalidOperationException("El cliente ya está activo o conserva un alquiler activo. Actualizá el listado antes de reactivar.");
            rentalId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            pending = Math.Max(0m, reader.GetDecimal(2));
        }
        var today = TimeHelper.GetArgentinaTime();
        if (!rentalId.HasValue) return new(null, 0, 0, 0, 0, 0, today);

        var movements = new List<AccountMovement>();
        using (var command = new SqlCommand(@"
            SELECT movement_id, movement_date, movement_type, concept, amount, payment_id
            FROM account_movements WITH (UPDLOCK, HOLDLOCK) WHERE rental_id = @rental_id;", connection, transaction))
        {
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId.Value });
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) movements.Add(new()
            {
                Id = reader.GetInt32(0), RentalId = rentalId.Value, MovementDate = reader.GetDateTime(1),
                MovementType = reader.GetString(2), Concept = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Amount = reader.GetDecimal(4), PaymentId = reader.IsDBNull(5) ? null : reader.GetInt32(5)
            });
        }
        var result = PaymentAllocationEngine.Allocate(movements);
        var rentDebt = result.Rows.Sum(row => row.UnpaidRent);
        var interestDebt = result.Rows.Sum(row => row.UnpaidInterests) + pending;
        var credit = result.UnallocatedCredits.Values.Sum();
        // The settlement must also reach any future debit already present in the closed contract.
        var settlementDate = movements.Select(m => m.MovementDate)
            .Concat(movements.Select(ClientMonthBalanceService.ResolveMonthStart)).Append(today).Max();
        return new(rentalId, rentDebt + interestDebt - credit, rentDebt, interestDebt, credit, pending, settlementDate);
    }

    private static string ValidateReactivationDecision(CreateClientDTO dto, ReactivationBalanceData data)
    {
        var action = dto.ReactivationBalanceAction?.Trim().ToLowerInvariant();
        if (action is not (null or "keep" or "zero"))
            throw new ArgumentException("Elegí mantener el saldo o reactivar con saldo cero.");
        if (data.NetDebt != 0m && (action == null || !dto.ExpectedReactivationBalance.HasValue))
            throw new InvalidOperationException("El cliente tiene saldo pendiente de decisión. Volvé a empezar la reactivación.");
        if (dto.ExpectedReactivationBalance.HasValue &&
            (dto.ExpectedReactivationBalance.Value != -data.NetDebt || dto.ExpectedReactivationRentalId != data.RentalId))
            throw new InvalidOperationException("El saldo del cliente cambió. Volvé a empezar la reactivación para revisar el importe actualizado.");
        return action ?? "keep";
    }

    private async Task ApplyReactivationBalanceAsync(ReactivationBalanceData data, int newRentalId, string action,
        DateTime startDate, SqlConnection connection, SqlTransaction transaction)
    {
        if (!data.RentalId.HasValue || (data.NetDebt == 0m && data.PendingSurcharge == 0m)) return;
        var oldRentalId = data.RentalId.Value;
        async Task Add(int rentalId, DateTime date, string type, string concept, decimal amount)
        {
            if (amount <= 0m) return;
            await accountMovementService.CreateAccountMovementTransactionAsync(new()
            {
                RentalId = rentalId, MovementDate = date, MovementType = type, Concept = concept, Amount = amount
            }, connection, transaction);
        }
        if (data.PendingSurcharge > 0m)
        {
            await Add(oldRentalId, data.SettlementDate, "DEBITO", "Interés por mora pendiente al reactivar", data.PendingSurcharge);
            await ResetPendingSurchargeAsync(oldRentalId, connection, transaction);
        }
        await Add(oldRentalId, data.SettlementDate, data.NetDebt > 0 ? "CREDITO" : "DEBITO",
            action == "keep" ? $"Traspaso de saldo al alquiler {newRentalId} por reactivación" : "Ajuste de saldo a cero por reactivación",
            Math.Abs(data.NetDebt));
        await _clientMonthBalanceService.RebuildForRentalTransactionAsync(oldRentalId, connection, transaction);

        if (action == "keep" && data.NetDebt != 0m)
        {
            // Keep principal and interest separate and let the shared engine apply any credit.
            var previousMonth = new DateTime(startDate.Year, startDate.Month, 1).AddMonths(-1);
            await Add(newRentalId, previousMonth, "DEBITO", $"Saldo anterior del alquiler {oldRentalId} por reactivación", data.RentDebt);
            await Add(newRentalId, previousMonth, "DEBITO", $"Interés por mora anterior del alquiler {oldRentalId} por reactivación", data.InterestDebt);
            await Add(newRentalId, previousMonth, "CREDITO", $"Saldo a favor del alquiler {oldRentalId} por reactivación", data.Credit);
        }
    }
}
