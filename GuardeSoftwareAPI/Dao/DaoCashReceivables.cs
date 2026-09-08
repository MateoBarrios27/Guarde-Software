using System.Data;
using GuardeSoftwareAPI.Dtos.Cash;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao;

public class DaoCashReceivables
{
    private readonly AccessDB _db;
    public DaoCashReceivables(AccessDB db) => _db = db;

    public async Task<List<CashReceivableDto>> GetAsync()
    {
        // A single statement keeps the displayed totals and payment history consistent.
        var table = await _db.GetTableAsync("Receivables", @"
            SELECT r.*, p.id payment_id, p.payment_date, p.amount, p.comment
            FROM dbo.cash_receivables r
            LEFT JOIN dbo.cash_receivable_payments p ON p.receivable_id = r.id
            ORDER BY r.agreement_date DESC, r.id DESC, p.payment_date DESC, p.id DESC");
        var result = new Dictionary<int, CashReceivableDto>();
        foreach (DataRow row in table.Rows)
        {
            int id = (int)row["id"];
            if (!result.TryGetValue(id, out var account))
            {
                account = new CashReceivableDto {
                    Id = id, Description = (string)row["description"],
                    Date = (DateTime)row["agreement_date"], TotalAmount = (decimal)row["total_amount"], Notes = (string)row["notes"]
                };
                result.Add(id, account);
            }
            if (row["payment_id"] != DBNull.Value)
            {
                account.Payments.Add(new CashReceivablePaymentDto {
                    Id = (int)row["payment_id"], Date = (DateTime)row["payment_date"],
                    Amount = (decimal)row["amount"], Comment = (string)row["comment"]
                });
                account.PaidAmount += (decimal)row["amount"];
            }
        }
        return result.Values.ToList();
    }

    private static SqlParameter Money(string name, decimal value) => new(name, SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = value };
    private static SqlParameter[] Fields(CashReceivableInput input) => new[] {
        new SqlParameter("@Description", SqlDbType.NVarChar, 500) { Value = input.Description.Trim() },
        new SqlParameter("@Date", SqlDbType.Date) { Value = input.Date.Date },
        Money("@Total", input.TotalAmount),
        new SqlParameter("@Notes", SqlDbType.NVarChar, 1000) { Value = input.Notes?.Trim() ?? "" }
    };

    public async Task<int> CreateAsync(CashReceivableInput input) => Convert.ToInt32(await _db.ExecuteScalarAsync(@"
        INSERT INTO dbo.cash_receivables(description, agreement_date, total_amount, notes)
        OUTPUT INSERTED.id VALUES(@Description, @Date, @Total, @Notes)", Fields(input)));

    // Every mutation of an existing account locks its parent first. Concurrent payments,
    // total edits and deletions therefore validate against the latest committed balance.
    private async Task<T> LockedAsync<T>(int id, Func<SqlConnection, SqlTransaction, decimal, Task<T>> action)
    {
        using var connection = _db.GetConnectionClose();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        using var command = new SqlCommand("SELECT total_amount FROM dbo.cash_receivables WITH (UPDLOCK, HOLDLOCK) WHERE id=@Id", connection, transaction);
        command.Parameters.AddWithValue("@Id", id);
        var total = await command.ExecuteScalarAsync();
        if (total == null) throw new KeyNotFoundException("La cuenta a cobrar ya no existe.");
        var result = await action(connection, transaction, (decimal)total);
        await transaction.CommitAsync();
        return result;
    }

    public Task<int> UpdateAsync(int id, CashReceivableInput input) => LockedAsync(id, async (connection, transaction, total) => {
        using var command = new SqlCommand(@"
            SELECT COALESCE(SUM(amount),0) FROM dbo.cash_receivable_payments WHERE receivable_id=@Id;
            SELECT MIN(payment_date) FROM dbo.cash_receivable_payments WHERE receivable_id=@Id", connection, transaction);
        command.Parameters.AddWithValue("@Id", id);
        using (var reader = await command.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            if (input.TotalAmount < reader.GetDecimal(0)) throw new InvalidOperationException("El total no puede ser menor a lo ya cobrado.");
            await reader.NextResultAsync();
            await reader.ReadAsync();
            if (!reader.IsDBNull(0) && input.Date.Date > reader.GetDateTime(0)) throw new InvalidOperationException("La fecha de la cuenta no puede ser posterior a sus pagos.");
        }
        command.CommandText = "UPDATE dbo.cash_receivables SET description=@Description, agreement_date=@Date, total_amount=@Total, notes=@Notes WHERE id=@Id";
        command.Parameters.AddRange(Fields(input));
        return await command.ExecuteNonQueryAsync();
    });

    public Task<int> DeleteAsync(int id) => LockedAsync(id, async (connection, transaction, total) => {
        using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.cash_receivable_payments WHERE receivable_id=@Id", connection, transaction);
        command.Parameters.AddWithValue("@Id", id);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0) throw new InvalidOperationException("La cuenta tiene pagos. Eliminá primero los pagos si necesitás borrar la cuenta.");
        command.CommandText = "DELETE FROM dbo.cash_receivables WHERE id=@Id";
        return await command.ExecuteNonQueryAsync();
    });

    public Task<int> AddPaymentAsync(int id, CashReceivablePaymentInput input) => LockedAsync(id, async (connection, transaction, total) => {
        using var command = new SqlCommand("SELECT id, receivable_id, amount, payment_date, comment FROM dbo.cash_receivable_payments WHERE request_id=@RequestId", connection, transaction);
        command.Parameters.AddWithValue("@RequestId", input.RequestId);
        using (var reader = await command.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                if (reader.GetInt32(1) != id || reader.GetDecimal(2) != input.Amount || reader.GetDateTime(3) != input.Date.Date || reader.GetString(4) != (input.Comment?.Trim() ?? ""))
                    throw new InvalidOperationException("El identificador corresponde a otro pago. Actualizá el historial antes de continuar.");
                return reader.GetInt32(0);
            }
        }
        command.Parameters.AddWithValue("@Id", id);
        command.CommandText = "SELECT COALESCE(SUM(amount),0) FROM dbo.cash_receivable_payments WHERE receivable_id=@Id";
        var paid = Convert.ToDecimal(await command.ExecuteScalarAsync());
        if (input.Amount > total - paid) throw new InvalidOperationException("El pago supera el saldo pendiente. Actualizá la cuenta para ver los últimos cobros.");
        command.CommandText = "SELECT agreement_date FROM dbo.cash_receivables WHERE id=@Id";
        if (input.Date.Date < (DateTime)(await command.ExecuteScalarAsync())!) throw new InvalidOperationException("El pago no puede ser anterior a la fecha de la cuenta.");
        command.CommandText = @"INSERT INTO dbo.cash_receivable_payments(receivable_id, payment_date, amount, comment, request_id)
            OUTPUT INSERTED.id VALUES(@Id, @Date, @Amount, @Comment, @RequestId)";
        command.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date) { Value = input.Date.Date });
        command.Parameters.Add(Money("@Amount", input.Amount));
        command.Parameters.Add(new SqlParameter("@Comment", SqlDbType.NVarChar, 500) { Value = input.Comment?.Trim() ?? "" });
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    });

    public Task<int> DeletePaymentAsync(int id, int paymentId) => LockedAsync(id, async (connection, transaction, total) => {
        using var command = new SqlCommand("DELETE FROM dbo.cash_receivable_payments WHERE id=@PaymentId AND receivable_id=@Id", connection, transaction);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@PaymentId", paymentId);
        if (await command.ExecuteNonQueryAsync() == 0) throw new KeyNotFoundException("El pago ya no existe en esta cuenta.");
        return 0;
    });
}
