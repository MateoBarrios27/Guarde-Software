using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GuardeSoftwareAPI.Dtos.Payment;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.payment;

public sealed class OfflineSyncException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Receipt and payment commit together. A retry never replays the financial mutation.</summary>
public static class OfflinePaymentReceiptStore
{
    public static string Hash(CreatePaymentTransaction dto)
    {
        static string? Money(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture);
        var intent = new {
            dto.ClientId, dto.PaymentMethodId, dto.MovementType, Concept = dto.Concept ?? "",
            Amount = Money(dto.Amount), Date = dto.Date.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            dto.IsAdvancePayment, dto.AdvanceMonths, CommissionAmount = Money(dto.CommissionAmount),
            CommissionConcept = dto.CommissionConcept ?? "", dto.SurchargeAction,
            SurchargeAmount = Money(dto.SurchargeAmount), dto.SurchargeAmountWasOverridden
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(intent))));
    }

    public static async Task<int?> FindAsync(CreatePaymentTransaction dto, SqlConnection connection, SqlTransaction transaction)
    {
        if (!dto.OfflineLocalId.HasValue) return null;
        using var ready = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.offline_payment_receipts', N'U') IS NULL THEN 0 ELSE 1 END", connection, transaction);
        if (Convert.ToInt32(await ready.ExecuteScalarAsync()) != 1)
            throw new OfflineSyncException("OFFLINE_SCHEMA_REQUIRED", "Falta aplicar Database/Update_Add_OfflinePaymentReceipts.sql. El cobro se conserva localmente.");
        using var acquire = new SqlCommand("""
            DECLARE @result INT;
            EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive',
                @LockOwner='Transaction', @LockTimeout=15000, @DbPrincipal='public';
            SELECT @result;
            """, connection, transaction);
        acquire.Parameters.Add("@resource", SqlDbType.NVarChar, 255).Value = "offline-payment:" + dto.OfflineLocalId.Value.ToString("D");
        if (Convert.ToInt32(await acquire.ExecuteScalarAsync()) < 0)
            throw new OfflineSyncException("PAYMENT_IN_PROGRESS", "Este cobro se está conciliando desde otra pestaña o equipo. Reintentá con el mismo registro.");
        using var lookup = new SqlCommand("""
            SELECT r.client_id, r.request_hash, r.payment_id,
                CASE WHEN EXISTS(SELECT 1 FROM dbo.payments p WHERE p.payment_id=r.payment_id) THEN 1 ELSE 0 END
            FROM dbo.offline_payment_receipts r WHERE r.local_id=@localId;
            """, connection, transaction);
        lookup.Parameters.Add("@localId", SqlDbType.UniqueIdentifier).Value = dto.OfflineLocalId.Value;
        using var reader = await lookup.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        if (reader.GetInt32(0) != dto.ClientId || !string.Equals(reader.GetString(1), dto.OfflineRequestHash, StringComparison.Ordinal))
            throw new OfflineSyncException("OFFLINE_PAYMENT_CHANGED", "Este identificador ya confirmó un cobro con otros datos. Revisá el registro; no se volvió a acreditar.");
        if (reader.GetInt32(3) == 0)
            throw new OfflineSyncException("OFFLINE_PAYMENT_DELETED", "Este cobro ya se registró y luego se eliminó. No se volvió a crear; revisalo en Finanzas.");
        return reader.GetInt32(2);
    }

    public static async Task SaveAsync(CreatePaymentTransaction dto, int paymentId, SqlConnection connection, SqlTransaction transaction)
    {
        if (!dto.OfflineLocalId.HasValue) return;
        using var command = new SqlCommand("""
            INSERT dbo.offline_payment_receipts(local_id, client_id, request_hash, payment_id)
            VALUES(@localId, @clientId, @hash, @paymentId);
            """, connection, transaction);
        command.Parameters.Add("@localId", SqlDbType.UniqueIdentifier).Value = dto.OfflineLocalId.Value;
        command.Parameters.Add("@clientId", SqlDbType.Int).Value = dto.ClientId;
        command.Parameters.Add("@hash", SqlDbType.Char, 64).Value = dto.OfflineRequestHash!;
        command.Parameters.Add("@paymentId", SqlDbType.Int).Value = paymentId;
        await command.ExecuteNonQueryAsync();
    }
}
