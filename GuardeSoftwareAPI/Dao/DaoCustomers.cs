using System;
using System.Data;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao
{

	public class DaoCustomers
	{
        private readonly AccessDB accessDB;

        public DaoCustomers(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetCustomers() {

            string consult = "SELECT customer_id, payment_identifier,first_name,last_name,registration_date,document_number,tax_id,preferred_payment_method_id,tax_condition, notes FROM customers WHERE active=1";

            return accessDB.GetTable("customers",consult);
        }

        public DataTable GetCustomerById(int id)
        {
            string consult = "SELECT customer_id, payment_identifier,first_name,last_name,registration_date,document_number,tax_id,preferred_payment_method_id,tax_condition, notes FROM customers WHERE customer_id = @customer_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@customer_id",SqlDbType.Int) {Value = id},
            };

            return accessDB.GetTable("customers",consult,parameters);
        }
    }
}

