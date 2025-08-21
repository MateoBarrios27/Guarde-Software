using System;


namespace GuardeSoftwareAPI.Dao
{
    public class DaoRentals
    {
        private readonly AccessDB accessDB;

        public DaoRentals(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public GetRentals()
        {
            string consult = "SELECT rental_id, customer_id, start_date, end_date, contracted_square_meters FROM rentals WHERE active = 1";
            
            return accessDB.GetTable("rentals",consult);
        }
    }
}
