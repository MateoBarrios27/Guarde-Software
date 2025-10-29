using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;
using System.Text;


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
                    l.locker_id,
                    lt.name AS locker_type,
                    l.identifier,
                    w.name AS warehouse,
                    lt.amount
                FROM
                    lockers l
                INNER JOIN
                    warehouses w ON l.warehouse_id = w.warehouse_id
                INNER JOIN
                    rentals r ON l.rental_id = r.rental_id
                INNER JOIN
                    locker_types lt ON l.locker_type_id = lt.locker_type_id
                WHERE
                    r.client_id = @client_id AND r.active = 1 AND l.active = 1";    

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

        public async Task<List<int>> GetLockerIdsByRentalIdTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            List<int> ids = new List<int>();
            string query = "SELECT locker_id FROM lockers WHERE rental_id = @rental_id AND active = 1"; // Solo activos
            SqlParameter[] parameters = { new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId } };

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        ids.Add(reader.GetInt32(0));
                    }
                }
            }
            return ids;
        }

        public async Task<int> UnassignLockersFromRentalTransactionAsync(List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerIds == null || !lockerIds.Any()) return 0;

            // Construir la cláusula IN dinámicamente para evitar SQL Injection
            var parameters = new List<SqlParameter>();
            var inClause = new StringBuilder();
            for (int i = 0; i < lockerIds.Count; i++)
            {
                string paramName = $"@lockerId{i}";
                inClause.Append(paramName).Append(i < lockerIds.Count - 1 ? "," : "");
                parameters.Add(new SqlParameter(paramName, SqlDbType.Int) { Value = lockerIds[i] });
            }

            // Cambia status a 'DISPONIBLE' al desasignar
            string query = $"UPDATE lockers SET rental_id = NULL, status = 'DISPONIBLE' WHERE locker_id IN ({inClause.ToString()})";

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters.ToArray());
                return await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<int> AssignLockersToRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerIds == null || !lockerIds.Any()) return 0;
             if (rentalId <= 0) throw new ArgumentException("Invalid rental ID for assignment.");

            var parameters = new List<SqlParameter>();
            parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });

            var inClause = new StringBuilder();
            for (int i = 0; i < lockerIds.Count; i++)
            {
                string paramName = $"@lockerId{i}";
                inClause.Append(paramName).Append(i < lockerIds.Count - 1 ? "," : "");
                parameters.Add(new SqlParameter(paramName, SqlDbType.Int) { Value = lockerIds[i] });
            }

            // Cambia status a 'OCUPADO' al asignar
            string query = $"UPDATE lockers SET rental_id = @rental_id, status = 'OCUPADO' WHERE locker_id IN ({inClause.ToString()}) AND rental_id IS NULL AND status = 'DISPONIBLE'"; // Doble check de seguridad

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters.ToArray());
                return await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<decimal> CalculateTotalM3ForLockersAsync(List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerIds == null || !lockerIds.Any()) return 0m;

            var parameters = new List<SqlParameter>();
            var inClause = new StringBuilder();
            for (int i = 0; i < lockerIds.Count; i++)
            {
                string paramName = $"@lockerId{i}";
                inClause.Append(paramName).Append(i < lockerIds.Count - 1 ? "," : "");
                parameters.Add(new SqlParameter(paramName, SqlDbType.Int) { Value = lockerIds[i] });
            }

            string query = $@"
                SELECT ISNULL(SUM(lt.m3), 0)
                FROM lockers l
                JOIN locker_types lt ON l.locker_type_id = lt.locker_type_id
                WHERE l.locker_id IN ({inClause.ToString()})";

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters.ToArray());
                object result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0m;
            }
        }
    }
}