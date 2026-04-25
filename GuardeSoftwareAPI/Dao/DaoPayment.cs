using System;
using System.Data;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoPayment
    {
        private readonly AccessDB accessDB;

        public DaoPayment(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        // public async Task<DataTable> GetPayments()
        // {
        //     string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments";

        //     return await accessDB.GetTableAsync("payments", query);
        // }

        //CAMBIAR ESTE METODO QUE TRAE SOLO LOS PAGOS DEL MES, HACERLO A PARTE
        public async Task<DataTable> GetPayments()
        {
            string query = @"
                SELECT 
                    p.payment_id,
                    p.client_id,
                    c.payment_identifier,
                    c.full_name,
                    p.payment_method_id,
                    p.payment_date,
                    p.amount
                FROM payments p
                INNER JOIN clients c ON p.client_id = c.client_id
                WHERE MONTH(p.payment_date) = MONTH(GETDATE())
                AND YEAR(p.payment_date) = YEAR(GETDATE())";

            return await accessDB.GetTableAsync("payments", query);
        }
        

        public async Task<DataTable> GetPaymentById(int id)
        {

            string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments WHERE payment_id = @payment_id";

            SqlParameter[] parameters = [

                new("@payment_id", SqlDbType.Int){Value  = id},
            ];

            return await accessDB.GetTableAsync("payments", query, parameters);
        }

        public async Task<bool> CreatePayment(Payment payment)
        {
            string query = "INSERT INTO payments (client_id, payment_method_id, payment_date, amount) VALUES (@client_id, @payment_method_id, @payment_date, @amount)";

            SqlParameter[] parameters = [
                new("@client_id", SqlDbType.Int){Value  = payment.ClientId},
                new("@payment_method_id", SqlDbType.Int){Value  = payment.PaymentMethodId},
                new("@payment_date", SqlDbType.DateTime){Value  = payment.PaymentDate},
                new("@amount", SqlDbType.Decimal){Value  = payment.Amount},
            ];

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<int> CreatePaymentTransactionAsync(Payment payment, SqlConnection connection, SqlTransaction transaction)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));

            string query = @"
                INSERT INTO payments (client_id, payment_method_id, payment_date, amount)
                OUTPUT INSERTED.payment_id
                VALUES (@client_id, @payment_method_id, @payment_date, @amount);";

            SqlParameter[] parameters =
            [
                new("@client_id", SqlDbType.Int) { Value = payment.ClientId },
                new("@payment_method_id", SqlDbType.Int) { Value = payment.PaymentMethodId },
                new("@payment_date", SqlDbType.DateTime) { Value = payment.PaymentDate },
                new("@amount", SqlDbType.Decimal) { Value = payment.Amount }
            ];

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);

            object result = await command.ExecuteScalarAsync() ?? DBNull.Value;

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("No se pudo obtener el ID del nuevo Payment.");

            return Convert.ToInt32(result);
        }

        public async Task<DataTable> GetPaymentsByClientId(int clientId)
        {

            string query = "SELECT payment_id, client_id, payment_method_id, payment_date, amount FROM payments WHERE client_id = @client_id";


            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id", SqlDbType.Int){Value  = clientId},
            };

            return await accessDB.GetTableAsync("payments", query, parameters);

        }

        public async Task<DataTable> GetDetailedPaymentsAsync()
        {
            string query = @"
                SELECT 
                    am.movement_id,
                    ISNULL(p.payment_id, 0) AS payment_id,
                    ISNULL(c_pay.full_name, c_rent.full_name) AS full_name,
                    ISNULL(c_pay.payment_identifier, c_rent.payment_identifier) AS payment_identifier,
                    ISNULL(c_pay.preferred_payment_method_id, c_rent.preferred_payment_method_id) AS preferred_payment_method_id,
                    am.amount,
                    am.movement_date AS payment_date,
                    ISNULL(pm.name, 'Ajuste / Manual') AS payment_method_name,
                    ISNULL(am.concept, 'Crédito a favor') AS concept,
                    am.movement_type
                FROM account_movements am
                LEFT JOIN payments p ON am.payment_id = p.payment_id
                LEFT JOIN payment_methods pm ON p.payment_method_id = pm.payment_method_id
                LEFT JOIN clients c_pay ON p.client_id = c_pay.client_id
                LEFT JOIN rentals r ON am.rental_id = r.rental_id
                LEFT JOIN clients c_rent ON r.client_id = c_rent.client_id
                WHERE am.movement_type = 'CREDITO' 
                ORDER BY am.movement_date DESC";

            return await accessDB.GetTableAsync("detailed_payments", query);
        }

    
        public async Task<bool> DeletePaymentTransactionAsync(int movementId)
        {
            using SqlConnection connection = accessDB.GetConnectionClose();
            await connection.OpenAsync();
            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                // 1. Buscamos si el movimiento tiene un pago (padre) asociado
                int? paymentIdToDelete = null;
                string getPaymentIdQuery = "SELECT payment_id FROM account_movements WHERE movement_id = @MovementId";
                
                using (var cmdCheck = new SqlCommand(getPaymentIdQuery, connection, transaction))
                {
                    cmdCheck.Parameters.Add(new SqlParameter("@MovementId", movementId));
                    var result = await cmdCheck.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        paymentIdToDelete = Convert.ToInt32(result);
                    }
                }

                int rowsAffected = 0;

                // 2. Ramificamos la lógica según el tipo de movimiento
                if (paymentIdToDelete.HasValue && paymentIdToDelete.Value > 0)
                {
                    // ESCENARIO A: Es un pago formal. 
                    // Primero asesinamos a TODOS los hijos (movimientos) atados a este pago
                    string deleteMovementsQuery = "DELETE FROM account_movements WHERE payment_id = @PaymentId";
                    using (var cmdMovements = new SqlCommand(deleteMovementsQuery, connection, transaction))
                    {
                        cmdMovements.Parameters.Add(new SqlParameter("@PaymentId", paymentIdToDelete.Value));
                        await cmdMovements.ExecuteNonQueryAsync();
                    }

                    // Segundo asesinamos al padre (el comprobante de pago)
                    string deletePaymentQuery = "DELETE FROM payments WHERE payment_id = @PaymentId";
                    using (var cmdPayment = new SqlCommand(deletePaymentQuery, connection, transaction))
                    {
                        cmdPayment.Parameters.Add(new SqlParameter("@PaymentId", paymentIdToDelete.Value));
                        rowsAffected = await cmdPayment.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // ESCENARIO B: Es un Ajuste/Crédito manual que no tiene pago padre.
                    // Simplemente borramos ese movimiento específico.
                    string deleteSingleMovementQuery = "DELETE FROM account_movements WHERE movement_id = @MovementId";
                    using (var cmdSingle = new SqlCommand(deleteSingleMovementQuery, connection, transaction))
                    {
                        cmdSingle.Parameters.Add(new SqlParameter("@MovementId", movementId));
                        rowsAffected = await cmdSingle.ExecuteNonQueryAsync();
                    }
                }

                transaction.Commit();
                return rowsAffected > 0;
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }


        
    }
}
