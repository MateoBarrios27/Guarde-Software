using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;


namespace GuardeSoftwareAPI.Dao
{

    public class DaoLocker
    {
        private readonly AccessDB accessDB;

        public DaoLocker(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public DataTable GetLockers()
        {
            string query = "SELECT locker_id, warehouse_id,locker_type_id, identifier, features, status, rental_id FROM lockers WHERE active = 1";

            return accessDB.GetTable("lockers", query);
        }

        public DataTable GetLockerById(int id)
        {

            string query = "SELECT locker_id, warehouse_id,locker_type_id, identifier, features, status, rental_id FROM lockers WHERE locker_id = @locker_id";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@locker_id", SqlDbType.Int){Value = id},
            };

            return accessDB.GetTable("lockers", query, parameters);
        }

        public DataTable GetLockersAvailable()
        {
            string query = "SELECT locker_id, warehouse_id,locker_type_id, identifier, features, status, rental_id FROM lockers WHERE active = 1 AND status = 'DISPONIBLE'";

            return accessDB.GetTable("lockers", query);
        }

        public bool CreateLocker(Locker locker)
        {

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@warehouse_id",SqlDbType.Int){Value = locker.WarehouseId},
                new SqlParameter("@locker_type_id",SqlDbType.Int){Value = locker.LockerTypeId},
                new SqlParameter("@identifier",SqlDbType.VarChar,100){Value = (object?)locker.Identifier ?? DBNull.Value},
                new SqlParameter("@features",SqlDbType.VarChar){Value = (object?)locker.Features ?? DBNull.Value},
                new SqlParameter("@status",SqlDbType.VarChar,50){Value = locker.Status},
            };

            string query = "INSERT INTO lockers(warehouse_id,locker_type_id, identifier, features, status)VALUES(@warehouse_id,@locker_type_id, @identifier, @features, @status)";

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }

        public async Task<bool> SetRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {

            string query = "UPDATE lockers SET rental_id = @rental_id, status = 'OCUPADO' WHERE locker_id = @locker_id";

            foreach (var lockerId in lockerIds)
            {
                SqlParameter[] parameters = new SqlParameter[] {

                    new SqlParameter("@locker_id", SqlDbType.Int){ Value =  lockerId},
                    new SqlParameter("@rental_id", SqlDbType.Int){ Value = rentalId},
                };

                using (var command = new SqlCommand(query, connection, transaction))
                {
                    command.Parameters.AddRange(parameters);
                    int rows = await command.ExecuteNonQueryAsync();

                    if (rows <= 0) return false;
                }
            }
            return true;
        }

        public async Task<DataTable> GetLockersByClientIdAsync(int clientId)
        {
           
            string query = @"
                SELECT 
                    lt.name AS locker_type, l.identifier, w.name AS warehouse
                FROM 
                    lockers l
                INNER JOIN 
                    warehouses w ON l.warehouse_id = w.warehouse_id
                INNER JOIN
                    rentals r ON l.rental_id = r.rental_id
                INNER JOIN
                    locker_types lt ON l.locker_type_id = lt.locker_type_id
                WHERE
                    r.client_id = 1 AND r.active = 1";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId }
            };

            return await accessDB.GetTableAsync("lockers_by_client", query, parameters);
        }

        public bool DeleteLocker(int id)
        {

            string query = "UPDATE lockers SET active = 0 WHERE locker_id = @locker_id";

            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@locker_id", SqlDbType.Int ) { Value = id},
            };

            return accessDB.ExecuteCommand(query, parameters) > 0;
        }
    }
}