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

        public DataTable GetAccountMovById(int id) {

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

            string query = "INSERT INTO account_movements(rental_id,movement_date,movement_type,concept,amount, payment_id)"
            + "VALUES(@rental_id,@movement_date,@movement_type,@concept,@amount, @payment_id)";

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }
    }         
}  
