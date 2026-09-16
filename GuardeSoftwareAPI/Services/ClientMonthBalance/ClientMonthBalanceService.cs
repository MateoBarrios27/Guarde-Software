using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
using GuardeSoftwareAPI.Services.payment;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Services.clientMonthBalance
{
    public class ClientMonthBalanceService : IClientMonthBalanceService
    {
        private static readonly Regex RentConceptRegex = new(
            @"Alquiler\s+(?<month>[A-Za-zÁÉÍÓÚáéíóúñÑ]+)\s+(?<year>\d{4})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly AccessDB _accessDB;
        private readonly DaoRental _daoRental;

        public ClientMonthBalanceService(AccessDB accessDB)
        {
            _accessDB = accessDB;
            _daoRental = new DaoRental(accessDB);
        }

        public async Task RebuildForRentalAsync(int rentalId)
        {
            using var connection = _accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                await RebuildForRentalTransactionAsync(rentalId, connection, transaction);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task RebuildAllActiveRentalsAsync()
        {
            var rentalIds = await _daoRental.GetActiveRentalsIdsAsync();

            foreach (var rentalId in rentalIds)
            {
                await RebuildForRentalAsync(rentalId);
            }
        }

        public async Task RebuildForRentalTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID.", nameof(rentalId));

            var movements = await GetMovementsAsync(rentalId, connection, transaction);
            var rebuiltRows = PaymentAllocationEngine.Allocate(movements).Rows;
            await ReplaceBalancesAsync(rentalId, rebuiltRows, connection, transaction);
        }

        internal static DateTime ResolveMonthStart(AccountMovement movement)
        {
            if (!string.IsNullOrWhiteSpace(movement.Concept))
            {
                var match = RentConceptRegex.Match(movement.Concept);
                if (match.Success)
                {
                    var monthName = Normalize(match.Groups["month"].Value);
                    if (TryMapSpanishMonth(monthName, out var monthNumber) &&
                        int.TryParse(match.Groups["year"].Value, out var year))
                    {
                        return new DateTime(year, monthNumber, 1);
                    }
                }
            }

            return new DateTime(movement.MovementDate.Year, movement.MovementDate.Month, 1);
        }

        internal static bool IsInterestConcept(string? concept)
        {
            var normalized = Normalize(concept ?? string.Empty);
            return normalized.Contains("interes por mora", StringComparison.Ordinal);
        }

        private static bool TryMapSpanishMonth(string normalizedMonth, out int monthNumber)
        {
            monthNumber = normalizedMonth switch
            {
                "enero" => 1,
                "febrero" => 2,
                "marzo" => 3,
                "abril" => 4,
                "mayo" => 5,
                "junio" => 6,
                "julio" => 7,
                "agosto" => 8,
                "septiembre" => 9,
                "setiembre" => 9,
                "octubre" => 10,
                "noviembre" => 11,
                "diciembre" => 12,
                _ => 0
            };

            return monthNumber > 0;
        }

        private static string Normalize(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        internal static async Task<List<AccountMovement>> GetMovementsAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"
                SELECT movement_id, rental_id, movement_date, movement_type, concept, amount, payment_id
                FROM account_movements
                WHERE rental_id = @rental_id
                ORDER BY movement_date ASC, movement_id ASC;";

            var movements = new List<AccountMovement>();
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                movements.Add(new AccountMovement
                {
                    Id = reader.GetInt32(0),
                    RentalId = reader.GetInt32(1),
                    MovementDate = reader.GetDateTime(2),
                    MovementType = reader.GetString(3),
                    Concept = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Amount = reader.GetDecimal(5),
                    PaymentId = reader.IsDBNull(6) ? null : reader.GetInt32(6)
                });
            }

            return movements;
        }

        private async Task ReplaceBalancesAsync(
            int rentalId,
            IEnumerable<ClientMonthBalance> balances,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            const string deleteQuery = "DELETE FROM client_month_balances WHERE rental_id = @rental_id;";
            using (var deleteCommand = new SqlCommand(deleteQuery, connection, transaction))
            {
                deleteCommand.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                await deleteCommand.ExecuteNonQueryAsync();
            }

            const string insertQuery = @"
                INSERT INTO client_month_balances
                (rental_id, month_year, previous_balance, interests, monthly_debits, balance, paid, advanced_payment, allocated_interests, allocated_rent)
                VALUES
                (@rental_id, @month_year, @previous_balance, @interests, @monthly_debits, @balance, @paid, @advanced_payment, @allocated_interests, @allocated_rent);";

            foreach (var balance in balances)
            {
                using var insertCommand = new SqlCommand(insertQuery, connection, transaction);
                insertCommand.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                insertCommand.Parameters.Add(new SqlParameter("@month_year", SqlDbType.VarChar, 7) { Value = balance.MonthYear });
                insertCommand.Parameters.Add(new SqlParameter("@previous_balance", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = balance.PreviousBalance });
                insertCommand.Parameters.Add(new SqlParameter("@interests", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = balance.Interests });
                insertCommand.Parameters.Add(new SqlParameter("@monthly_debits", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = balance.MonthlyDebits });
                insertCommand.Parameters.Add(new SqlParameter("@balance", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = balance.Balance });
                insertCommand.Parameters.Add(new SqlParameter("@paid", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = balance.Paid });
                insertCommand.Parameters.Add(new SqlParameter("@advanced_payment", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = balance.AdvancedPayment });
                insertCommand.Parameters.Add(new SqlParameter("@allocated_interests", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = balance.AllocatedInterests ?? 0m });
                insertCommand.Parameters.Add(new SqlParameter("@allocated_rent", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = balance.AllocatedRent ?? 0m });
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

    }
}
