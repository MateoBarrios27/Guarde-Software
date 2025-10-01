using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;


namespace GuardeSoftwareAPI.Dao
{

    public class DaoLocker
    {
        private readonly AccessDB accessDB;

        public DaoLocker(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetLockers()
        {
            string query = "SELECT locker_id, warehouse_id,locker_type_id, identifier, features, status, rental_id FROM lockers WHERE active = 1";

            return await accessDB.GetTableAsync("lockers", query);
        }

        public async Task<DataTable> GetLockerById(int id)
        {

            string query = "SELECT locker_id, warehouse_id,locker_type_id, identifier, features, status, rental_id FROM lockers WHERE locker_id = @locker_id AND active = 1";

            SqlParameter[] parameters = new SqlParameter[] {

                new SqlParameter("@locker_id", SqlDbType.Int){Value = id},
            };

            return await accessDB.GetTableAsync("lockers", query, parameters);
        }

        public async Task<DataTable> GetLockersAvailable()
        {
            string query = "SELECT locker_id, warehouse_id,locker_type_id, identifier, features, status, rental_id FROM lockers WHERE active = 1 AND status = 'DISPONIBLE'";

            return await accessDB.GetTableAsync("lockers", query);
        }

        public async Task<Locker> CreateLocker(Locker locker)
        {

            SqlParameter[] parameters =
            [
                new SqlParameter("@warehouse_id",SqlDbType.Int){Value = locker.WarehouseId},
                new SqlParameter("@locker_type_id",SqlDbType.Int){Value = locker.LockerTypeId},
                new SqlParameter("@identifier",SqlDbType.VarChar,100){Value = (object?)locker.Identifier ?? DBNull.Value},
                new SqlParameter("@features",SqlDbType.VarChar){Value = (object?)locker.Features ?? DBNull.Value},
                new SqlParameter("@status",SqlDbType.VarChar,50){Value = locker.Status},
            ];

            string query = "INSERT INTO lockers(warehouse_id,locker_type_id, identifier, features, status)VALUES(@warehouse_id,@locker_type_id, @identifier, @features, @status); SELECT SCOPE_IDENTITY();";

             object newId = await accessDB.ExecuteScalarAsync(query, parameters);

            if (newId != null && newId != DBNull.Value)
            {
                //Assign the newly generated ID to the locker object
                locker.Id = Convert.ToInt32(newId);
            }

            return locker;
        }

        public async Task<bool> SetRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {

            string query = "UPDATE lockers SET rental_id = @rental_id, status = 'OCUPADO' WHERE locker_id = @locker_id";

            foreach (var lockerId in lockerIds)
            {
                SqlParameter[] parameters = [

                    new SqlParameter("@locker_id", SqlDbType.Int){ Value =  lockerId},
                    new SqlParameter("@rental_id", SqlDbType.Int){ Value = rentalId},
                ];

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

            SqlParameter[] parameters =
            [
                new SqlParameter("@client_id", SqlDbType.Int) { Value = clientId }
            ];

            return await accessDB.GetTableAsync("lockers_by_client", query, parameters);
        }

        public async Task<bool> DeleteLocker(int id)
        {

            string query = "UPDATE lockers SET active = 0 WHERE locker_id = @locker_id";

            SqlParameter[] parameters =
            [
                new SqlParameter("@locker_id", SqlDbType.Int ) { Value = id},
            ];

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }

        public async Task<bool> IsLockerIsAvailabeAsync(int lockerId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM lockers WHERE locker_id = @locker_id AND status = 'DISPONIBLE'";
            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@locker_id", SqlDbType.Int) { Value = lockerId });
                int count = (int)await command.ExecuteScalarAsync();
                return count > 0;
            }
        }

        public async Task<bool> UpdateLocker(Locker locker)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@locker_id",SqlDbType.Int) { Value = locker.Id },
                new SqlParameter("@identifier",SqlDbType.VarChar,100){Value = (object?)locker.Identifier ?? DBNull.Value},
                new SqlParameter("@features",SqlDbType.VarChar){Value = (object?)locker.Features ?? DBNull.Value},
                new SqlParameter("@status",SqlDbType.VarChar,50){Value = locker.Status},
            };

            string query = "UPDATE lockers SET identifier = @identifier, features = @features, status = @status WHERE locker_id = @locker_id";

            return await accessDB.ExecuteCommandAsync(query,parameters) > 0;   
        }

        public async Task<bool> UpdateLockerStatus(int lockerId, string status)
        {
            SqlParameter[] parameters =
            [
                new SqlParameter("@locker_id",SqlDbType.Int) {Value = lockerId},
                new SqlParameter("@status",SqlDbType.VarChar,50){Value = status},
            ];

            string query = "UPDATE lockers SET status = @status WHERE locker_id = @locker_id";

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }
    }
}