using System;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoAccountMov
	{
        private readonly AccessDB accessDB;

        public DaoAccountMov(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public GetAccountMov()
        {
            string consult = "SELECT movement_id, rental_id,movement_date,movement_type,concept,payment_id FROM account_movements";

            return accessDB.GetTable("account_movements",consult);
        }
    }
}
