using System;


namespace GuardeSoftwareAPI.Dao
{

	public class DaoLockerTypes
	{
        private readonly AccessDB accessDB;

        public DaoLockerTypes(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public GetLockerTypes()
        {
            string consult = "SELECT locker_type_id, name, amount, cubic_meters  FROM locker_types WHERE active = 1";

            return accessDB.GetTable("locker_types",consult);
        }
    }
}
