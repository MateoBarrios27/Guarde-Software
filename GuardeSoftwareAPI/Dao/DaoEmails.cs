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
            string consult = "SELECT email_id, customer_id, email, type FROM emails";

            return accessDB.GetTable("emails", consult);
        }
    }
}
