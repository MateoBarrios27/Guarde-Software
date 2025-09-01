using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;


namespace GuardeSoftwareAPI.Dao
{

	public class DaoClient
	{
        private readonly AccessDB accessDB;

        public DaoClient(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetClients() {

            string query = "SELECT client_id, payment_identifier,first_name,last_name,registration_date,dni,cuit,preferred_payment_method_id,iva_condition, notes FROM clients WHERE active=1";

            return accessDB.GetTable("clients", query);
        }

        public DataTable GetClientById(int id)
        {
            string query = "SELECT client_id, payment_identifier,first_name,last_name,registration_date,dni,cuit,preferred_payment_method_id,iva_condition, notes FROM clients WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@client_id",SqlDbType.Int) {Value = id},
            };

            return accessDB.GetTable("clients", query, parameters);
        }

        public bool CreateClient(Client client)
        {
            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@payment_identifier",SqlDbType.Decimal) {Value = client.PaymentIdentifier},
                new SqlParameter("@first_name",SqlDbType.VarChar) {Value = client.FirstName },
                new SqlParameter("@last_name",SqlDbType.VarChar) {Value = client.LastName},
                new SqlParameter("@registration_date",SqlDbType.DateTime) {Value = client.RegistrationDate},
                new SqlParameter("@dni",SqlDbType.VarChar) {Value = client.Dni},
                new SqlParameter("@cuit",SqlDbType.VarChar) {Value = client.Cuit},
                new SqlParameter("@preferred_payment_method_id",SqlDbType.Int) {Value = client.PreferredPaymentMethodId},
                new SqlParameter("@iva_condition",SqlDbType.VarChar) {Value = client.IvaCondition},
                new SqlParameter("@notes",SqlDbType.VarChar) {Value = client.Notes},
            };

            string query = "INSERT INTO clients(payment_identifier,first_name,last_name,registration_date,dni,cuit,preferred_payment_method_id,iva_condition, notes)"
            + "VALUES(@payment_identifier,@first_name,@last_name,@registration_date,@dni,@cuit,@preferred_payment_method_id,@iva_condition, @notes)";

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }


        public bool DeleteClientById(int id) {

            string query = "UPDATE clients SET active = 0 WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@client_id", SqlDbType.Int){Value = id},
            };
            
            return accessDB.ExecuteCommand(query, parameters) > 0;
        
        }
    }
}

