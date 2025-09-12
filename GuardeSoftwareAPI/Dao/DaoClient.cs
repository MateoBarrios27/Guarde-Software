using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;


namespace GuardeSoftwareAPI.Dao
{

    public class DaoClient
    {
        private readonly AccessDB accessDB;

        public DaoClient(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetClients()
        {

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


        public bool DeleteClientById(int id)
        {

            string query = "UPDATE clients SET active = 0 WHERE client_id = @client_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@client_id", SqlDbType.Int){Value = id},
            };

            return accessDB.ExecuteCommand(query, parameters) > 0;

        }

        public async Task<int> CreateClientAsync(Client client)
        {

            SqlParameter[] parameters = new SqlParameter[]
            {

                new SqlParameter("@payment_identifier", SqlDbType.Decimal)
                {
                    Precision = 10,
                    Scale = 2,
                    Value = (object?)client.PaymentIdentifier ?? DBNull.Value
                },
                new SqlParameter("@first_name", SqlDbType.VarChar) { Value = (object?)client.FirstName?.Trim() ?? DBNull.Value },
                new SqlParameter("@last_name", SqlDbType.VarChar) { Value = (object?)client.LastName?.Trim() ?? DBNull.Value },
                new SqlParameter("@registration_date", SqlDbType.DateTime) { Value = client.RegistrationDate },
                new SqlParameter("@dni", SqlDbType.VarChar) { Value = (object?)client.Dni?.Trim() ?? DBNull.Value },
                new SqlParameter("@cuit", SqlDbType.VarChar) { Value = (object?)client.Cuit?.Trim() ?? DBNull.Value },
                new SqlParameter("@preferred_payment_method_id", SqlDbType.Int)
                {
                    Value = client.PreferredPaymentMethodId > 0 ? (object)client.PreferredPaymentMethodId : DBNull.Value
                },
                new SqlParameter("@iva_condition", SqlDbType.VarChar) { Value = (object?)client.IvaCondition?.Trim() ?? DBNull.Value },
                new SqlParameter("@notes", SqlDbType.VarChar) { Value = (object?)client.Notes?.Trim() ?? DBNull.Value },
            };

            // Important: OUTPUT INSERTED.id returns the id even though there are triggers. 
            // instead: Identity scope can return the wrong ID if there are triggers
            string query = @"
            INSERT INTO clients(payment_identifier, first_name, last_name, registration_date, dni, cuit, preferred_payment_method_id, iva_condition, notes)
            OUTPUT INSERTED.client_id
            VALUES(@payment_identifier, @first_name, @last_name, @registration_date, @dni, @cuit, @preferred_payment_method_id, @iva_condition, @notes);";

            object result = await accessDB.ExecuteScalarAsync(query, parameters);

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("The newly added customer id could not be returned.");

            return Convert.ToInt32(result);
        }


        //METHOD FOR TRANSACTION
        public async Task<int> CreateClientTransactionAsync(Client client, SqlConnection connection, SqlTransaction transaction)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@payment_identifier", SqlDbType.Decimal)
                {
                    Precision = 10,
                    Scale = 2,
                    Value = (object?)client.PaymentIdentifier ?? DBNull.Value
                },
                new SqlParameter("@first_name", SqlDbType.VarChar) { Value = (object?)client.FirstName?.Trim() ?? DBNull.Value },
                new SqlParameter("@last_name", SqlDbType.VarChar) { Value = (object?)client.LastName?.Trim() ?? DBNull.Value },
                new SqlParameter("@registration_date", SqlDbType.DateTime) { Value = client.RegistrationDate },
                new SqlParameter("@dni", SqlDbType.VarChar) { Value = (object?)client.Dni?.Trim() ?? DBNull.Value },
                new SqlParameter("@cuit", SqlDbType.VarChar) { Value = (object?)client.Cuit?.Trim() ?? DBNull.Value },
                new SqlParameter("@preferred_payment_method_id", SqlDbType.Int)
                {
                    Value = client.PreferredPaymentMethodId > 0 ? (object)client.PreferredPaymentMethodId : DBNull.Value
                },
                new SqlParameter("@iva_condition", SqlDbType.VarChar) { Value = (object?)client.IvaCondition?.Trim() ?? DBNull.Value },
                new SqlParameter("@notes", SqlDbType.VarChar) { Value = (object?)client.Notes?.Trim() ?? DBNull.Value },
            };

            string query = @"
                            INSERT INTO clients(payment_identifier, first_name, last_name, registration_date, dni, cuit, preferred_payment_method_id, iva_condition, notes)
                            OUTPUT INSERTED.client_id
                            VALUES(@payment_identifier, @first_name, @last_name, @registration_date, @dni, @cuit, @preferred_payment_method_id, @iva_condition, @notes);";

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                object result = await command.ExecuteScalarAsync() ?? DBNull.Value;

                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException("The newly added customer id could not be returned.");

                return Convert.ToInt32(result);
            }
        }
        
        //Here missing the method to get balance and payment status
        public async Task<DataTable> GetClientDetailByIdAsync(int id)
        {
            string query = @"
                SELECT 
                    c.client_id, c.payment_identifier, c.first_name, c.last_name, c.registration_date,
                    c.dni, c.cuit, c.iva_condition, c.notes,
                    em.address, ph.number, ad.street, ad.city, ad.province,
                    pm.name AS preferred_payment_method,
                    r.contracted_m3, cir.end_date, ir.frequency, ir.percentage
                FROM 
                    clients c
                LEFT JOIN
                    addresses ad ON c.client_id = ad.client_id
                LEFT JOIN
                    emails em ON c.client_id = em.client_id
                LEFT JOIN 
                    phones ph ON c.client_id = ph.client_id
                LEFT JOIN
                    clients_x_increase_regimens cir ON c.client_id = cir.client_id
                LEFT JOIN
                    increase_regimens ir ON cir.regimen_id = ir.regimen_id
                LEFT JOIN 
                    payment_methods pm ON c.preferred_payment_method_id = pm.payment_method_id
                LEFT JOIN 
                    rentals r ON c.client_id = r.client_id AND r.active = 1 
                WHERE 
                    c.client_id = @client_id AND c.active = 1;";

            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@client_id", SqlDbType.Int) { Value = id },
            };

            return await accessDB.GetTableAsync("client_details", query, parameters);
        }
    }
}

