using System;
using System.Data;


namespace GuardeSoftwareAPI.Dao { 

    public class DaoLockers
	{
        private readonly AccessDB accessDB;

        public DaoLockers(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetLockers()
        {
            string consult = "SELECT locker_id, warehouse_id,locker_type_id, identifier, features, status FROM lockers";

            return accessDB.GetTable("lockers",consult);
        }
    }
}