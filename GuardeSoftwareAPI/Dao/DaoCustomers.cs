using System;

namespace GuardeSoftwareAPI.Dao
{

	public class DaoCustomers
	{
        private readonly AccessDB accessDB;

        public DaoCustomers(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public GetCustomers() {

            string consult = "SELECT customer_id, payment_identifier,first_name,last_name,registration_date,document_number,tax_id,preferred_payment_method_id,tax_condition  notes  FROM WHERE active=1";

            return accessDB.GetTable("customers",consult);
        }

    }
}

