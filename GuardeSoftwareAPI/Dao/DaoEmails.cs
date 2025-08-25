using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoEmails
    {
        private readonly AccessDB accessDB;

        public DaoEmails(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetEmails()
        {
            string query = "SELECT email_id, client_id, address, type FROM emails";

            return accessDB.GetTable("emails", query);
        }
    }
}
