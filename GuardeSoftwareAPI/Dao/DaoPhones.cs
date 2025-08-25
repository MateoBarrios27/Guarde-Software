using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao
{
	public class DaoPhones
	{
        private readonly AccessDB accessDB;

        public DaoPhones(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetPhones()
        {
            string query = "SELECT phone_id, client_id, number, type, whatsapp FROM phones"
;
            return accessDB.GetTable("phones", query);
        }
    }
}