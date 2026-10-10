using System.Data;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.payment;

internal static class PaymentCollectionDecisionStore
{
    public static async Task<DateTime?> ReadAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
    {
        using var command = new SqlCommand("SELECT dbo.GetPaymentCollectionMonth(@RentalId)", connection, transaction);
        command.Parameters.Add("@RentalId", SqlDbType.Int).Value = rentalId;
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : Convert.ToDateTime(result);
    }

    public static async Task SaveAsync(int paymentId, int rentalId, DateTime? month, string? action,
        SqlConnection connection, SqlTransaction transaction)
    {
        using var command = new SqlCommand(@"
            INSERT dbo.payment_collection_decisions(payment_id,rental_id,next_payment_month,action,ledger_anchor_id)
            SELECT @PaymentId,@RentalId,@Month,@Action,ISNULL(MAX(movement_id),0)
            FROM dbo.account_movements WHERE rental_id=@RentalId;", connection, transaction);
        command.Parameters.Add("@PaymentId", SqlDbType.Int).Value = paymentId;
        command.Parameters.Add("@RentalId", SqlDbType.Int).Value = rentalId;
        command.Parameters.Add("@Month", SqlDbType.Date).Value = (object?)month ?? DBNull.Value;
        command.Parameters.Add("@Action", SqlDbType.NVarChar, 40).Value = action ?? "automatic";
        await command.ExecuteNonQueryAsync();
    }
}
