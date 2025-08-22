using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoRentals
    {
        private readonly AccessDB accessDB;

        public DaoRentals(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetRentals()
        {
            string consult = "SELECT rental_id, client_id, start_date, end_date, contracted_square_meters FROM rentals WHERE active = 1";
            
            return accessDB.GetTable("rentals",consult);
        }
    }
}
