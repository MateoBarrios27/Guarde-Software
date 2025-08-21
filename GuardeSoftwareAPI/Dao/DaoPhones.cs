using System;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoPhones
	{
        private readonly AccessDB accessDB;

        public DaoPhones(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public GetPhones()
        {
            string consult = "SELECT phone_id, customer_id, type, whatsapp FROM phones"
;
            return accessDB.GetTable("phones",consult);
        }
    }
}