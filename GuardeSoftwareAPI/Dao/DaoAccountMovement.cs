using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoAccountMovement
    {
        private readonly AccessDB accessDB;

        public DaoAccountMovement(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetAccountMovement()
        {
            string query = "SELECT movement_id, rental_id,movement_date,movement_type,concept,amount, payment_id FROM account_movements";

            return await accessDB.GetTableAsync("account_movements", query);
        }

        public async Task<DataTable> GetAccountMovById(int id)
        {

            string query = "SELECT movement_id, rental_id,movement_date,movement_type,concept,amount, payment_id FROM account_movements WHERE movement_id = @movement_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@movement_id", SqlDbType.Int) {Value = id},
            };

            return await accessDB.GetTableAsync("account_movements", query, parameters);
        }

        public async Task<DataTable> GetAccountMovByRentalId(int id)
        {
            string query = "SELECT movement_id, rental_id,movement_date,movement_type,concept,amount, payment_id FROM account_movements WHERE rental_id = @rental_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@rental_id", SqlDbType.Int) {Value = id},
            };

            return await accessDB.GetTableAsync("account_movements", query, parameters);

        }

        public async Task<bool> CreateAccountMovement(AccountMovement accountMovement)
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

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        //using for payment transaction
        public async Task<bool> CreateAccountMovementTransactionAsync(AccountMovement accountMovement, SqlConnection connection, SqlTransaction transaction)
        {
            if (accountMovement == null) throw new ArgumentNullException(nameof(accountMovement));

            string query = @"
                INSERT INTO account_movements (rental_id, movement_date, movement_type, concept, amount, payment_id)
                VALUES (@rental_id, @movement_date, @movement_type, @concept, @amount, @payment_id);";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new("@rental_id", SqlDbType.Int) { Value = accountMovement.RentalId },
                new("@movement_date", SqlDbType.DateTime) { Value = accountMovement.MovementDate },
                new("@movement_type", SqlDbType.NVarChar) { Value = accountMovement.MovementType },
                new("@concept", SqlDbType.NVarChar) { Value = accountMovement.Concept },
                new("@amount", SqlDbType.Decimal) { Value = accountMovement.Amount },
                new("@payment_id", SqlDbType.Int) { Value = accountMovement.PaymentId as object ?? DBNull.Value }
            };

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);

            int affectedRows = await command.ExecuteNonQueryAsync();
            return affectedRows > 0;
        }


        //This method checks if a debit entry exists for the current month for a given rental ID
        //It returns true if the entry debit exists, otherwise false.
        //Is used in DebitService and is a verification
       public async Task<bool> CheckIfDebitExistsByConceptAsync(int rentalId, string concept, SqlConnection connection)
        {
            string query = @"
                SELECT COUNT(1) 
                FROM account_movements 
                WHERE rental_id = @rentalId 
                  AND movement_type = 'DEBITO' 
                  AND concept LIKE @concept + '%'"; // Busca que empiece con el concepto (ej: 'Alquiler Enero 2025')

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.Add(new SqlParameter("@rentalId", SqlDbType.Int) { Value = rentalId });
                // Pasamos el concepto que esperamos encontrar (ej: "Alquiler Febrero 2025")
                command.Parameters.Add(new SqlParameter("@concept", SqlDbType.NVarChar) { Value = concept });

                object result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) > 0 : false;
            }
        }

        public async Task CreateDebitAsync(AccountMovement debit, SqlConnection connection)
        {
            string query = @"
                INSERT INTO account_movements (rental_id, movement_date, movement_type, concept, amount, payment_id)
                VALUES (@rental_id, @movement_date, @movement_type, @concept, @amount, @payment_id);";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@rental_id", SqlDbType.Int) { Value = debit.RentalId },
                new SqlParameter("@movement_date", SqlDbType.DateTime) { Value = debit.MovementDate },
                new SqlParameter("@movement_type", SqlDbType.VarChar) { Value = debit.MovementType },
                new SqlParameter("@concept", SqlDbType.VarChar) { Value = (object?)debit.Concept ?? DBNull.Value }, // Manejo de nulos
                new SqlParameter("@amount", SqlDbType.Decimal) { Value = debit.Amount },
                new SqlParameter("@payment_id", SqlDbType.Int) { Value = debit.PaymentId as object ?? DBNull.Value }
            };

            // NO usamos accessDB.ExecuteCommandAsync, usamos un SqlCommand con la conexión existente
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddRange(parameters);
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<bool> DeleteAccountMovementByIdAsync(int movementId)
        {
            string query = "DELETE FROM account_movements WHERE movement_id = @movement_id";
            
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@movement_id", SqlDbType.Int) { Value = movementId }
            };

            // ExecuteCommandAsync devuelve el número de filas afectadas
            int rowsAffected = await accessDB.ExecuteCommandAsync(query, parameters);
            return rowsAffected > 0;
        }
        
    }         
}  
