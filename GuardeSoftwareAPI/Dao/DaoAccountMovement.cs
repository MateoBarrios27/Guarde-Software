using System;
using System.Data;


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
            string consult = "SELECT movement_id, rental_id,movement_date, movement_type, concept, amount, payment_id FROM account_movements";

            return accessDB.GetTable("account_movements",consult);
        }
    }
}
