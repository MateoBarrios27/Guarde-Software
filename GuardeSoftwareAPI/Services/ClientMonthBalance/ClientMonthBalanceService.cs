using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Entities;
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
            var histories = await GetRentalAmountHistoryAsync(rentalId, connection, transaction);

            var debitBuckets = BuildDebitBuckets(movements);
            var credits = BuildCredits(movements);

            ProjectFutureMonthsIfNeeded(debitBuckets, credits, histories);

            var rebuiltRows = ApplyCreditsChronologically(debitBuckets, credits);
            await ReplaceBalancesAsync(rentalId, rebuiltRows, connection, transaction);
        }

        private static SortedDictionary<DateTime, ClientMonthBalance> BuildDebitBuckets(IEnumerable<AccountMovement> movements)
        {
            var buckets = new SortedDictionary<DateTime, ClientMonthBalance>();

            foreach (var movement in movements.Where(m => string.Equals(m.MovementType, "DEBITO", StringComparison.OrdinalIgnoreCase)))
            {
                var monthStart = ResolveMonthStart(movement);
                if (!buckets.TryGetValue(monthStart, out var bucket))
                {
                    bucket = new ClientMonthBalance
                    {
                        MonthYear = monthStart.ToString("MM/yyyy"),
                        PreviousBalance = 0m,
                        Interests = 0m,
                        MonthlyDebits = 0m,
                        Paid = 0m,
                        AdvancedPayment = 0m
                    };
                    buckets[monthStart] = bucket;
                }

                if (IsInterestConcept(movement.Concept))
                    bucket.Interests += movement.Amount;
                else
                    bucket.MonthlyDebits += movement.Amount;
            }

            return buckets;
        }

        private static List<CreditLedger> BuildCredits(IEnumerable<AccountMovement> movements)
        {
            return movements
                .Where(m => string.Equals(m.MovementType, "CREDITO", StringComparison.OrdinalIgnoreCase))
                .Select(m => new CreditLedger
                {
                    Remaining = m.Amount,
                    CreditMonth = new DateTime(m.MovementDate.Year, m.MovementDate.Month, 1)
                })
                .Where(c => c.Remaining > 0)
                .OrderBy(c => c.CreditMonth)
                .ToList();
        }

        private static List<ClientMonthBalance> ApplyCreditsChronologically(
            SortedDictionary<DateTime, ClientMonthBalance> debitBuckets,
            List<CreditLedger> credits)
        {
            var result = new List<ClientMonthBalance>();
            var orderedMonths = debitBuckets.Keys.OrderBy(d => d).ToList();
            var creditIndex = 0;
            decimal carryDebt = 0m;

            foreach (var monthStart in orderedMonths)
            {
                var row = debitBuckets[monthStart];
                row.PreviousBalance = carryDebt;
                row.Balance = row.PreviousBalance + row.Interests + row.MonthlyDebits;
                row.Paid = 0m;
                row.AdvancedPayment = 0m;

                var outstanding = row.Balance;
                while (outstanding > 0 && creditIndex < credits.Count)
                {
                    var credit = credits[creditIndex];
                    if (credit.Remaining <= 0)
                    {
                        creditIndex++;
                        continue;
                    }

                    var applied = Math.Min(outstanding, credit.Remaining);
                    if (monthStart > credit.CreditMonth)
                        row.AdvancedPayment += applied;
                    else
                        row.Paid += applied;

                    outstanding -= applied;
                    credit.Remaining -= applied;

                    if (credit.Remaining <= 0)
                        creditIndex++;
                }

                carryDebt = Math.Max(0m, row.Balance - row.Paid - row.AdvancedPayment);
                row.Balance = row.PreviousBalance + row.Interests + row.MonthlyDebits;
                result.Add(row);
            }

            return result;
        }

        private static void ProjectFutureMonthsIfNeeded(
            SortedDictionary<DateTime, ClientMonthBalance> debitBuckets,
            List<CreditLedger> credits,
            List<RentalAmountHistory> histories)
        {
            var totalCredits = credits.Sum(c => c.Remaining);
            if (totalCredits <= 0 || histories.Count == 0) return;

            decimal totalCharges = debitBuckets.Values.Sum(b => b.MonthlyDebits + b.Interests);
            DateTime cursor = debitBuckets.Count > 0
                ? debitBuckets.Keys.Max()
                : credits.Min(c => c.CreditMonth);

            while (totalCharges < totalCredits)
            {
                cursor = cursor.AddMonths(1);
                var rentAmount = ResolveRentAmountForMonth(cursor, histories);
                if (rentAmount <= 0) break;

                if (!debitBuckets.ContainsKey(cursor))
                {
                    debitBuckets[cursor] = new ClientMonthBalance
                    {
                        MonthYear = cursor.ToString("MM/yyyy"),
                        PreviousBalance = 0m,
                        Interests = 0m,
                        MonthlyDebits = rentAmount,
                        Paid = 0m,
                        AdvancedPayment = 0m
                    };
                }
                else
                {
                    debitBuckets[cursor].MonthlyDebits += rentAmount;
                }

                totalCharges += rentAmount;
            }
        }

        private static DateTime ResolveMonthStart(AccountMovement movement)
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

        private static bool IsInterestConcept(string? concept)
        {
            var normalized = Normalize(concept ?? string.Empty);
            return normalized.Contains("interes por mora", StringComparison.Ordinal);
        }

        private static decimal ResolveRentAmountForMonth(DateTime monthStart, IEnumerable<RentalAmountHistory> histories)
        {
            var targetDate = monthStart.Date;
            var history = histories
                .Where(h => h.StartDate.Date <= targetDate && (!h.EndDate.HasValue || h.EndDate.Value.Date >= targetDate))
                .OrderByDescending(h => h.StartDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefault();

            return history?.Amount ?? 0m;
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

        private async Task<List<AccountMovement>> GetMovementsAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
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

        private async Task<List<RentalAmountHistory>> GetRentalAmountHistoryAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = @"
                SELECT rental_amount_history_id, rental_id, amount, start_date, end_date
                FROM rental_amount_history
                WHERE rental_id = @rental_id
                ORDER BY start_date ASC, rental_amount_history_id ASC;";

            var histories = new List<RentalAmountHistory>();
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                histories.Add(new RentalAmountHistory
                {
                    Id = reader.GetInt32(0),
                    RentalId = reader.GetInt32(1),
                    Amount = reader.GetDecimal(2),
                    StartDate = reader.GetDateTime(3),
                    EndDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                });
            }

            return histories;
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
                (rental_id, month_year, previous_balance, interests, monthly_debits, balance, paid, advanced_payment)
                VALUES
                (@rental_id, @month_year, @previous_balance, @interests, @monthly_debits, @balance, @paid, @advanced_payment);";

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
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        private sealed class CreditLedger
        {
            public DateTime CreditMonth { get; set; }
            public decimal Remaining { get; set; }
        }
    }
}
