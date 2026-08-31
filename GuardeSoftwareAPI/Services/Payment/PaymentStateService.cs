using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GuardeSoftwareAPI.Dao;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.payment
{
    public sealed class PaymentStateSnapshot
    {
        public int ClientId { get; init; }
        public int RentalId { get; init; }
        public string Token { get; init; } = string.Empty;
        public DateTime CapturedAtUtc { get; init; }
    }

    public interface IPaymentStateService
    {
        Task<PaymentStateSnapshot> GetSnapshotAsync(
            int clientId,
            SqlConnection? connection = null,
            SqlTransaction? transaction = null);
    }

    public sealed class PaymentStateService : IPaymentStateService
    {
        private readonly AccessDB _accessDb;

        public PaymentStateService(AccessDB accessDb)
        {
            _accessDb = accessDb;
        }

        public async Task<PaymentStateSnapshot> GetSnapshotAsync(
            int clientId,
            SqlConnection? connection = null,
            SqlTransaction? transaction = null)
        {
            if (clientId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(clientId));
            }

            var ownsConnection = connection == null;
            connection ??= _accessDb.GetConnectionClose();

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                var lockHint = transaction == null ? string.Empty : " WITH (UPDLOCK, HOLDLOCK)";
                var fingerprint = new StringBuilder();

                var rentalId = await AppendRentalStateAsync(clientId, connection, transaction, lockHint, fingerprint);
                await AppendRowsAsync(
                    $@"SELECT id, month_year, previous_balance, interests, monthly_debits, balance, paid, advanced_payment
                       FROM client_month_balances{lockHint}
                       WHERE rental_id = @rentalId
                       ORDER BY id",
                    connection,
                    transaction,
                    rentalId,
                    fingerprint);
                await AppendRowsAsync(
                    $@"SELECT rental_amount_history_id, amount, start_date, end_date
                       FROM rental_amount_history{lockHint}
                       WHERE rental_id = @rentalId
                       ORDER BY rental_amount_history_id",
                    connection,
                    transaction,
                    rentalId,
                    fingerprint);
                await AppendRowsAsync(
                    $@"SELECT movement_id, movement_date, movement_type, concept, amount, payment_id
                       FROM account_movements{lockHint}
                       WHERE rental_id = @rentalId
                       ORDER BY movement_id",
                    connection,
                    transaction,
                    rentalId,
                    fingerprint);

                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint.ToString()));

                return new PaymentStateSnapshot
                {
                    ClientId = clientId,
                    RentalId = rentalId,
                    Token = Convert.ToHexString(bytes),
                    CapturedAtUtc = DateTime.UtcNow
                };
            }
            finally
            {
                if (ownsConnection)
                {
                    await connection.DisposeAsync();
                }
            }
        }

        private static async Task<int> AppendRentalStateAsync(
            int clientId,
            SqlConnection connection,
            SqlTransaction? transaction,
            string lockHint,
            StringBuilder fingerprint)
        {
            const string queryPrefix = @"
                SELECT TOP (1)
                    rental_id,
                    client_id,
                    start_date,
                    end_date,
                    contracted_m3,
                    months_unpaid,
                    increase_anchor_date,
                    pending_surcharge,
                    pending_surcharge_rent_base,
                    pending_surcharge_period,
                    price_lock_end_date,
                    active
                FROM rentals";
            var query = $"{queryPrefix}{lockHint} WHERE client_id = @clientId AND active = 1 ORDER BY start_date DESC, rental_id DESC";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@clientId", SqlDbType.Int) { Value = clientId });
            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("El cliente no tiene un alquiler activo.");
            }

            for (var column = 0; column < reader.FieldCount; column++)
            {
                AppendValue(fingerprint, reader.IsDBNull(column) ? null : reader.GetValue(column));
            }

            return reader.GetInt32(0);
        }

        private static async Task AppendRowsAsync(
            string query,
            SqlConnection connection,
            SqlTransaction? transaction,
            int rentalId,
            StringBuilder fingerprint)
        {
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rentalId", SqlDbType.Int) { Value = rentalId });
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                for (var column = 0; column < reader.FieldCount; column++)
                {
                    AppendValue(fingerprint, reader.IsDBNull(column) ? null : reader.GetValue(column));
                }

                fingerprint.Append(';');
            }

            fingerprint.Append('#');
        }

        private static void AppendValue(StringBuilder builder, object? value)
        {
            switch (value)
            {
                case null:
                case DBNull:
                    builder.Append("<null>");
                    break;
                case DateTime date:
                    builder.Append(date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                    break;
                case decimal amount:
                    builder.Append(amount.ToString("0.############################", CultureInfo.InvariantCulture));
                    break;
                default:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }

            builder.Append('|');
        }
    }
}
