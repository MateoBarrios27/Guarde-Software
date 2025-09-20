using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoAccountMovement
    {
        private readonly AccessDB accessDB;

        public DaoAccountMovement(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetAccountMovement()
        {
            string query = "SELECT movement_id, rental_id,movement_date,movement_type,concept,amount, payment_id FROM account_movements";

            return accessDB.GetTable("account_movements", query);
        }

        public DataTable GetAccountMovById(int id)
        {

            string query = "SELECT movement_id, rental_id,movement_date,movement_type,concept,amount, payment_id FROM account_movements WHERE movement_id = @movement_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@movement_id", SqlDbType.Int) {Value = id},
            };

            return accessDB.GetTable("account_movements", query, parameters);
        }

        public DataTable GetAccountMovByRentalId(int id)
        {
            string query = "SELECT movement_id, rental_id,movement_date,movement_type,concept,amount, payment_id FROM account_movements WHERE rental_id = @rental_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@rental_id", SqlDbType.Int) {Value = id},
            };

            return accessDB.GetTable("account_movements", query, parameters);

        }

        public bool CreateAccountMovement(AccountMovement accountMovement)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@rental_id",SqlDbType.Int) { Value = accountMovement.RentalId },
                new SqlParameter("@movement_date",SqlDbType.DateTime) { Value = accountMovement.MovementDate },
                new SqlParameter("@movement_type",SqlDbType.NVarChar) { Value = accountMovement.MovementType },
                new SqlParameter("@concept", SqlDbType.NVarChar) { Value = accountMovement.Concept },
                new SqlParameter("@amount", SqlDbType.Decimal) { Value = accountMovement.Amount },
                new SqlParameter("@payment_id",SqlDbType.Int) { Value = accountMovement.PaymentId }
            };

            string query = "INSERT INTO account_movements(rental_id, movement_date, movement_type, concept, amount, payment_id)"
            + "VALUES(@rental_id, @movement_date, @movement_type, @concept, @amount, @payment_id)";

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }

        //This method checks if a debit entry exists for the current month for a given rental ID
        //It returns true if the entry debit exists, otherwise false.
        //Is used in DebitService and is a verification
        public async Task<bool> CheckIfDebitExistsForCurrentMonthAsync(int rentalId)
        {
            string query = @"
                SELECT COUNT(1) 
                FROM account_movements 
                WHERE rental_id = @rentalId 
                  AND movement_type = 'DEBITO' 
                  AND MONTH(movement_date) = MONTH(GETDATE()) 
                  AND YEAR(movement_date) = YEAR(GETDATE());";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@rentalId", SqlDbType.Int) { Value = rentalId }
            };

            object result = await accessDB.ExecuteScalarAsync(query, parameters);

            return Convert.ToInt32(result) > 0;
        }

        public async Task CreateDebitAsync(AccountMovement debit)
        {
            string query = @"
                INSERT INTO account_movements (rental_id, movement_date, movement_type, concept,amount, payment_id)
                VALUES (@rental_id, @movement_date, @movement_type, @concept, @amount, @payment_id);";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@rental_id",SqlDbType.Int) { Value = debit.RentalId},
                new SqlParameter("@movement_date", SqlDbType.DateTime) { Value = debit.MovementDate },
                new SqlParameter("@movement_type", SqlDbType.VarChar) { Value = debit.MovementType },
                new SqlParameter("@concept", SqlDbType.VarChar) { Value = debit.Concept },
                new SqlParameter("@amount", SqlDbType.Decimal) { Value = debit.Amount },
                // Validation for possible nulls for optional foreign key
                new SqlParameter("@payment_id", SqlDbType.Int) { Value = (object)debit.PaymentId ?? DBNull.Value }
            };


            await accessDB.ExecuteCommandAsync(query, parameters);
        }
        
        // Método para obtener los alquileres con saldo deudor al día de hoy
        // public async Task<DataTable> GetRentalsWithPositiveBalanceAsync()
        // {
        //     // Esta consulta usa los CTEs que ya conocemos para encontrar clientes
        //     // con un balance > 0, y nos trae su monto de alquiler actual para calcular el interés.
        //     string query = @"
        //         WITH CurrentRentalAmount AS (
        //             SELECT rental_id, amount as CurrentRent
        //             FROM (
        //                 SELECT 
        //                     rental_id, 
        //                     amount, 
        //                     ROW_NUMBER() OVER(PARTITION BY rental_id ORDER BY start_date DESC) as rn
        //                 FROM rental_amount_history
        //             ) as sub
        //             WHERE rn = 1
        //         ),
        //         AccountSummary AS (
        //             SELECT rental_id, SUM(CASE WHEN movement_type = 'DEBITO' THEN amount ELSE -amount END) AS Balance
        //             FROM account_movements
        //             GROUP BY rental_id
        //         )
        //         SELECT 
        //             r.rental_id,
        //             cra.CurrentRent
        //         FROM rentals r
        //         JOIN AccountSummary acc ON r.rental_id = acc.rental_id
        //         JOIN CurrentRentalAmount cra ON r.rental_id = cra.rental_id
        //         WHERE 
        //             r.active = 1
        //             AND acc.Balance > 0; -- La condición clave: solo traemos a los que deben dinero.
        //     ";

        //     return await accessDB.GetTableAsync("debtor_rentals", query);
        // }

        // // Método para crear un nuevo débito por interés en la cuenta del cliente
        // public async Task CreateInterestDebitAsync(int rentalId, decimal interestAmount, string concept)
        // {
        //     string query = @"
        //         INSERT INTO account_movements (rental_id, movement_date, movement_type, concept, amount)
        //         VALUES (@rental_id, @movement_date, 'DEBITO', @concept, @amount);";
            
        //     var parameters = new SqlParameter[]
        //     {
        //         new SqlParameter("@rental_id", rentalId),
        //         new SqlParameter("@movement_date", DateTime.Now.Date),
        //         new SqlParameter("@concept", concept),
        //         new SqlParameter("@amount", interestAmount)
        //     };

        //     await accessDB.ExecuteCommandAsync(query, parameters);
        // }
    }         
}  
