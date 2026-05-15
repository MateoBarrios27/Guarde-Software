using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoClientMonthBalance
    {
        private readonly AccessDB accessDB;

        public DaoClientMonthBalance(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task CreateMonthBalanceTransactionAsync(ClientMonthBalance balance, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO client_month_balances 
                (rental_id, month_year, previous_balance, interests, monthly_debits, balance, paid, advanced_payment)
                VALUES 
                (@rental_id, @month_year, @previous_balance, @interests, @monthly_debits, (@previous_balance + @interests + @monthly_debits), @paid, @advanced_payment)";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@rental_id", balance.RentalId);
            cmd.Parameters.AddWithValue("@month_year", balance.MonthYear);
            cmd.Parameters.AddWithValue("@previous_balance", balance.PreviousBalance);
            cmd.Parameters.AddWithValue("@interests", balance.Interests);
            cmd.Parameters.AddWithValue("@monthly_debits", balance.MonthlyDebits);
            cmd.Parameters.AddWithValue("@paid", balance.Paid);
            cmd.Parameters.AddWithValue("@advanced_payment", balance.AdvancedPayment);
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpsertMonthBalanceTransactionAsync(ClientMonthBalance balance, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                IF EXISTS (SELECT 1 FROM client_month_balances WHERE rental_id = @rental_id AND month_year = @month_year)
                BEGIN
                    UPDATE client_month_balances 
                    SET paid = paid + @paid, 
                        advanced_payment = advanced_payment + @advanced_payment, 
                        monthly_debits = @monthly_debits, 
                        balance = previous_balance + interests + @monthly_debits
                    WHERE rental_id = @rental_id AND month_year = @month_year;
                END
                ELSE
                BEGIN
                    INSERT INTO client_month_balances 
                    (rental_id, month_year, previous_balance, interests, monthly_debits, balance, paid, advanced_payment)
                    VALUES 
                    (@rental_id, @month_year, @previous_balance, @interests, @monthly_debits, (@previous_balance + @interests + @monthly_debits), @paid, @advanced_payment);
                END";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@rental_id", balance.RentalId);
            cmd.Parameters.AddWithValue("@month_year", balance.MonthYear);
            cmd.Parameters.AddWithValue("@previous_balance", balance.PreviousBalance);
            cmd.Parameters.AddWithValue("@interests", balance.Interests);
            cmd.Parameters.AddWithValue("@monthly_debits", balance.MonthlyDebits);
            cmd.Parameters.AddWithValue("@paid", balance.Paid);
            cmd.Parameters.AddWithValue("@advanced_payment", balance.AdvancedPayment);
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<decimal> GetRemanenteDelMesTransactionAsync(int rentalId, string monthYear, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "SELECT (paid + advanced_payment) - balance AS Remanente FROM client_month_balances WHERE rental_id = @rental_id AND month_year = @month_year";
            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.AddWithValue("@rental_id", rentalId);
            cmd.Parameters.AddWithValue("@month_year", monthYear);
            
            object result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
        }
    }
}