using System.Data;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.payment;

internal sealed record PaymentCollectionState(DateTime? Month, DateTime? ArrearsMonth);

internal static class PaymentCollectionDecisionStore
{
    public static async Task<PaymentCollectionState> ReadAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
    {
        using var command = new SqlCommand("SELECT dbo.GetPaymentCollectionMonth(@RentalId), dbo.GetPaymentArrearsMonth(@RentalId)", connection, transaction);
        command.Parameters.Add("@RentalId", SqlDbType.Int).Value = rentalId;
        using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new(reader.IsDBNull(0) ? null : reader.GetDateTime(0),
            reader.IsDBNull(1) ? null : reader.GetDateTime(1));
    }

    public static async Task SaveAsync(int paymentId, int rentalId, DateTime? month, DateTime? arrearsMonth, string? action,
        SqlConnection connection, SqlTransaction transaction)
    {
        using var command = new SqlCommand(@"
            INSERT dbo.payment_collection_decisions(payment_id,rental_id,next_payment_month,arrears_month,action,ledger_anchor_id)
            SELECT @PaymentId,@RentalId,@Month,@ArrearsMonth,@Action,ISNULL(MAX(movement_id),0)
            FROM dbo.account_movements WHERE rental_id=@RentalId;", connection, transaction);
        command.Parameters.Add("@PaymentId", SqlDbType.Int).Value = paymentId;
        command.Parameters.Add("@RentalId", SqlDbType.Int).Value = rentalId;
        command.Parameters.Add("@Month", SqlDbType.Date).Value = (object?)month ?? DBNull.Value;
        command.Parameters.Add("@ArrearsMonth", SqlDbType.Date).Value = (object?)arrearsMonth ?? DBNull.Value;
        command.Parameters.Add("@Action", SqlDbType.NVarChar, 40).Value = action ?? "automatic";
        await command.ExecuteNonQueryAsync();
    }
}
