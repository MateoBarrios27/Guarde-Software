using System;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoCustomersIncrease
	{
        private readonly AccessDB accessDB;

        public DaoCustomersIncrease(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public GetCustomersIncrease()
        {
            string consult = "SELECT customer_id, policy_id, start_date, end_date, FROM customers_increase_policies";

            return accessDB.GetTable("customers_increase_policies", consult);
        }
    }
}
