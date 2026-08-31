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
            // Para bauleras normales: el cliente viene del rental_id en la tabla lockers.
            // Para espacios libres: los clientes vienen de rental_lockers (múltiples).
            string query = @"
                SELECT 
                    l.locker_id, 
                    l.warehouse_id, 
                    l.locker_type_id, 
                    l.identifier, 
                    l.features, 
                    l.status, 
                    l.rental_id,
                    l.is_free_space,
                    -- Cliente para bauleras normales
                    CASE WHEN l.is_free_space = 0 THEN c.full_name ELSE NULL END AS client_name,
                    -- Clientes concatenados para espacios libres
                    CASE WHEN l.is_free_space = 1 THEN (
                        SELECT STRING_AGG(cl2.full_name, ', ')
                        FROM rental_lockers rl
                        INNER JOIN rentals r2 ON rl.rental_id = r2.rental_id AND r2.active = 1
                        INNER JOIN clients cl2 ON r2.client_id = cl2.client_id
                        WHERE rl.locker_id = l.locker_id
                    ) ELSE NULL END AS client_names
                FROM lockers l 
                LEFT JOIN rentals r ON l.rental_id = r.rental_id AND r.active = 1
                LEFT JOIN clients c ON r.client_id = c.client_id 
                WHERE l.active = 1";

            return await accessDB.GetTableAsync("lockers", query);
        }

        public async Task<DataTable> GetLockerById(int id)
        {
            string query = @"
                SELECT 
                    l.locker_id, l.warehouse_id, l.locker_type_id, l.identifier,
                    l.features, l.status, l.rental_id, l.is_free_space
                FROM lockers l
                WHERE l.locker_id = @locker_id AND l.active = 1";

            SqlParameter[] parameters = new SqlParameter[] {
                new SqlParameter("@locker_id", SqlDbType.Int){Value = id},
            };

            return await accessDB.GetTableAsync("lockers", query, parameters);
        }

        public async Task<DataTable> GetLockersAvailable()
        {
            // Un espacio libre es "disponible" para asignar SIEMPRE que su status != 'OCUPADO'.
            // Una baulera normal es disponible cuando status = 'DISPONIBLE'.
            string query = @"
                SELECT locker_id, warehouse_id, locker_type_id, identifier, features, status, rental_id, is_free_space
                FROM lockers 
                WHERE active = 1 
                  AND (
                      (is_free_space = 0 AND status = 'DISPONIBLE')
                      OR
                      (is_free_space = 1 AND status != 'OCUPADO')
                  )";

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
                new SqlParameter("@is_free_space",SqlDbType.Bit){Value = locker.IsFreeSpace},
            ];

            string query = "INSERT INTO lockers(warehouse_id, locker_type_id, identifier, features, status, is_free_space) VALUES(@warehouse_id, @locker_type_id, @identifier, @features, @status, @is_free_space); SELECT SCOPE_IDENTITY();";

             object newId = await accessDB.ExecuteScalarAsync(query, parameters);

            if (newId != null && newId != DBNull.Value)
            {
                locker.Id = Convert.ToInt32(newId);
            }

            return locker;
        }

        /// <summary>
        /// Asigna lockers a un rental. Para espacios libres inserta en rental_lockers (sin cambiar status).
        /// Para bauleras normales, actualiza rental_id y status = 'OCUPADO'.
        /// </summary>
        public async Task<bool> SetRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            foreach (var lockerId in lockerIds)
            {
                // Determinar si es espacio libre
                bool isFreeSpace = await IsLockerFreeSpaceAsync(lockerId, connection, transaction);

                if (isFreeSpace)
                {
                    // Insertar en rental_lockers (si no existe ya)
                    string insertQuery = @"
                        IF NOT EXISTS (SELECT 1 FROM rental_lockers WHERE rental_id = @rental_id AND locker_id = @locker_id)
                           AND EXISTS (
                               SELECT 1
                               FROM lockers
                               WHERE locker_id = @locker_id
                                 AND active = 1
                                 AND is_free_space = 1
                                 AND status <> 'OCUPADO'
                           )
                            INSERT INTO rental_lockers (rental_id, locker_id) VALUES (@rental_id, @locker_id)";
                    SqlParameter[] parameters = [
                        new SqlParameter("@rental_id", SqlDbType.Int){ Value = rentalId },
                        new SqlParameter("@locker_id", SqlDbType.Int){ Value = lockerId },
                    ];
                    using (var command = new SqlCommand(insertQuery, connection, transaction))
                    {
                        command.Parameters.AddRange(parameters);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // Lógica original: UPDATE rental_id y status
                    string updateQuery = "UPDATE lockers SET rental_id = @rental_id, status = 'OCUPADO' WHERE locker_id = @locker_id";
                    SqlParameter[] parameters = [
                        new SqlParameter("@locker_id", SqlDbType.Int){ Value = lockerId },
                        new SqlParameter("@rental_id", SqlDbType.Int){ Value = rentalId },
                    ];
                    using (var command = new SqlCommand(updateQuery, connection, transaction))
                    {
                        command.Parameters.AddRange(parameters);
                        int rows = await command.ExecuteNonQueryAsync();
                        if (rows <= 0) return false;
                    }
                }
            }
            return true;
        }

        private async Task<bool> IsLockerFreeSpaceAsync(int lockerId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT ISNULL(is_free_space, 0) FROM lockers WHERE locker_id = @locker_id";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@locker_id", SqlDbType.Int) { Value = lockerId });
            object result = await command.ExecuteScalarAsync();
            return result != null && result != DBNull.Value && Convert.ToBoolean(result);
        }

        public async Task<DataTable> GetLockersByClientIdAsync(int clientId)
        {
            string query = @"
                -- Bauleras normales asignadas via rental_id
                SELECT
                    l.locker_id,
                    lt.name AS locker_type,
                    l.identifier,
                    l.features,
                    w.name AS warehouse
                FROM lockers l
                INNER JOIN warehouses w ON l.warehouse_id = w.warehouse_id
                INNER JOIN rentals r ON l.rental_id = r.rental_id
                INNER JOIN locker_types lt ON l.locker_type_id = lt.locker_type_id
                WHERE r.client_id = @client_id AND r.active = 1 AND l.active = 1 AND l.is_free_space = 0

                UNION ALL

                -- Espacios libres asignados via rental_lockers
                SELECT
                    l.locker_id,
                    lt.name AS locker_type,
                    l.identifier,
                    l.features,
                    w.name AS warehouse
                FROM rental_lockers rl
                INNER JOIN lockers l ON rl.locker_id = l.locker_id
                INNER JOIN warehouses w ON l.warehouse_id = w.warehouse_id
                INNER JOIN locker_types lt ON l.locker_type_id = lt.locker_type_id
                INNER JOIN rentals r ON rl.rental_id = r.rental_id
                WHERE r.client_id = @client_id AND r.active = 1 AND l.active = 1

                UNION ALL

                -- Histórico (clientes inactivos)
                SELECT DISTINCT
                    l.locker_id,
                    lt.name AS locker_type,
                    l.identifier,
                    l.features,
                    w.name AS warehouse
                FROM client_locker_history clh
                INNER JOIN lockers l ON clh.locker_id = l.locker_id
                INNER JOIN warehouses w ON l.warehouse_id = w.warehouse_id
                INNER JOIN locker_types lt ON l.locker_type_id = lt.locker_type_id
                INNER JOIN clients c ON clh.client_id = c.client_id
                WHERE clh.client_id = @client_id
                  AND c.active = 0
                  AND ABS(DATEDIFF(day, ISNULL(clh.end_date, clh.start_date), (
                      SELECT MAX(ISNULL(clh2.end_date, clh2.start_date))
                      FROM client_locker_history clh2
                      WHERE clh2.client_id = @client_id
                  ))) <= 1";

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
            // Espacios libres: disponible si status != 'OCUPADO'
            // Bauleras normales: disponible si status = 'DISPONIBLE'
            const string query = @"
                SELECT COUNT(1) FROM lockers 
                WHERE locker_id = @locker_id
                  AND active = 1
                  AND (
                      (is_free_space = 1 AND status != 'OCUPADO')
                      OR
                      (is_free_space = 0 AND status = 'DISPONIBLE')
                  )";
            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@locker_id", SqlDbType.Int) { Value = lockerId });
                int count = (int)await command.ExecuteScalarAsync();
                return count > 0;
            }
        }

        public async Task<bool> UpdateLocker(Locker locker)
        {
            SqlParameter[] parameters =
            [
                new("@locker_id",SqlDbType.Int) { Value = locker.Id },
                new("@identifier",SqlDbType.VarChar,100){Value = (object?)locker.Identifier ?? DBNull.Value},
                new("@features",SqlDbType.VarChar){Value = (object?)locker.Features ?? DBNull.Value},
                new("@status",SqlDbType.VarChar,50){Value = locker.Status},
                new("@locker_type_id",SqlDbType.Int){Value = locker.LockerTypeId},
                new("@warehouse_id",SqlDbType.Int){Value = locker.WarehouseId},
                new("@is_free_space",SqlDbType.Bit){Value = locker.IsFreeSpace},
            ];

            string query = "UPDATE lockers SET identifier = @identifier, features = @features, status = @status, locker_type_id = @locker_type_id, warehouse_id = @warehouse_id, is_free_space = @is_free_space WHERE locker_id = @locker_id";

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;   
        }

        public async Task<bool> UpdateLockerTransactionAsync(Locker locker, bool unassignRental, SqlConnection connection, SqlTransaction transaction)
        {
            SqlParameter[] parameters =
            [
                new("@locker_id",SqlDbType.Int) { Value = locker.Id },
                new("@identifier",SqlDbType.VarChar,100){Value = (object?)locker.Identifier ?? DBNull.Value},
                new("@features",SqlDbType.VarChar){Value = (object?)locker.Features ?? DBNull.Value},
                new("@status",SqlDbType.VarChar,50){Value = locker.Status},
                new("@locker_type_id",SqlDbType.Int){Value = locker.LockerTypeId},
                new("@warehouse_id",SqlDbType.Int){Value = locker.WarehouseId},
                new("@is_free_space",SqlDbType.Bit){Value = locker.IsFreeSpace},
            ];

            string query = unassignRental
                ? "UPDATE lockers SET identifier = @identifier, features = @features, status = @status, locker_type_id = @locker_type_id, warehouse_id = @warehouse_id, is_free_space = @is_free_space, rental_id = NULL WHERE locker_id = @locker_id"
                : "UPDATE lockers SET identifier = @identifier, features = @features, status = @status, locker_type_id = @locker_type_id, warehouse_id = @warehouse_id, is_free_space = @is_free_space WHERE locker_id = @locker_id";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
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

        /// <summary>
        /// Obtiene los locker_ids asignados a un rental.
        /// Para bauleras normales: via rental_id en lockers.
        /// Para espacios libres: via rental_lockers.
        /// </summary>
        public async Task<List<int>> GetLockerIdsByRentalIdTransactionAsync(int rentalId, SqlConnection connection, SqlTransaction transaction)
        {
            List<int> ids = new List<int>();
            // Combinar ambas fuentes
            string query = @"
                SELECT locker_id FROM lockers WHERE rental_id = @rental_id AND active = 1 AND is_free_space = 0
                UNION
                SELECT locker_id FROM rental_lockers WHERE rental_id = @rental_id";

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

        /// <summary>
        /// Desasigna lockers de un rental. Espacios libres: elimina de rental_lockers.
        /// Bauleras normales: pone rental_id = NULL y status = 'DISPONIBLE'.
        /// </summary>
        public async Task<int> UnassignLockersFromRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerIds == null || !lockerIds.Any()) return 0;

            int totalAffected = 0;

            foreach (var lockerId in lockerIds)
            {
                bool isFreeSpace = await IsLockerFreeSpaceAsync(lockerId, connection, transaction);

                if (isFreeSpace)
                {
                    // Para espacios libres, eliminar de rental_lockers (no cambiamos status)
                    string deleteQuery = "DELETE FROM rental_lockers WHERE locker_id = @locker_id AND rental_id = @rental_id";
                    using (var cmd = new SqlCommand(deleteQuery, connection, transaction))
                    {
                        cmd.Parameters.Add(new SqlParameter("@locker_id", SqlDbType.Int) { Value = lockerId });
                        cmd.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                        totalAffected += await cmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // Baulera normal: limpiar rental_id y poner disponible
                    string updateQuery = "UPDATE lockers SET rental_id = NULL, status = 'DISPONIBLE' WHERE locker_id = @locker_id";
                    using (var cmd = new SqlCommand(updateQuery, connection, transaction))
                    {
                        cmd.Parameters.Add(new SqlParameter("@locker_id", SqlDbType.Int) { Value = lockerId });
                        totalAffected += await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            return totalAffected;
        }

        /// <summary>
        /// Desasigna un espacio libre de un rental específico (sin afectar otras asignaciones).
        /// </summary>
        public async Task<int> UnassignFreeSpaceFromRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerIds == null || !lockerIds.Any()) return 0;

            int totalAffected = 0;
            foreach (var lockerId in lockerIds)
            {
                string deleteQuery;
                if (rentalId <= 0)
                {
                    // rentalId = 0 significa eliminar TODAS las asignaciones del locker
                    deleteQuery = "DELETE FROM rental_lockers WHERE locker_id = @locker_id";
                    using (var cmd = new SqlCommand(deleteQuery, connection, transaction))
                    {
                        cmd.Parameters.Add(new SqlParameter("@locker_id", SqlDbType.Int) { Value = lockerId });
                        totalAffected += await cmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // Eliminar sólo el rental específico
                    deleteQuery = "DELETE FROM rental_lockers WHERE rental_id = @rental_id AND locker_id = @locker_id";
                    using (var cmd = new SqlCommand(deleteQuery, connection, transaction))
                    {
                        cmd.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                        cmd.Parameters.Add(new SqlParameter("@locker_id", SqlDbType.Int) { Value = lockerId });
                        totalAffected += await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            return totalAffected;
        }

        public async Task<int> AssignLockersToRentalTransactionAsync(int rentalId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerIds == null || !lockerIds.Any()) return 0;
            if (rentalId <= 0) throw new ArgumentException("Invalid rental ID for assignment.");

            int totalAffected = 0;

            foreach (var lockerId in lockerIds)
            {
                bool isFreeSpace = await IsLockerFreeSpaceAsync(lockerId, connection, transaction);

                if (isFreeSpace)
                {
                    // Espacio libre: insertar en rental_lockers
                    string insertQuery = @"
                        IF NOT EXISTS (SELECT 1 FROM rental_lockers WHERE rental_id = @rental_id AND locker_id = @locker_id)
                           AND EXISTS (
                               SELECT 1
                               FROM lockers
                               WHERE locker_id = @locker_id
                                 AND active = 1
                                 AND is_free_space = 1
                                 AND status <> 'OCUPADO'
                           )
                        BEGIN
                            INSERT INTO rental_lockers (rental_id, locker_id) VALUES (@rental_id, @locker_id);
                            SELECT 1;
                        END
                        ELSE SELECT 0;";

                    using (var cmd = new SqlCommand(insertQuery, connection, transaction))
                    {
                        cmd.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                        cmd.Parameters.Add(new SqlParameter("@locker_id", SqlDbType.Int) { Value = lockerId });
                        object result = await cmd.ExecuteScalarAsync();
                        totalAffected += (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
                    }
                }
                else
                {
                    // Baulera normal: UPDATE con doble check
                    string updateQuery = "UPDATE lockers SET rental_id = @rental_id, status = 'OCUPADO' WHERE locker_id = @locker_id AND rental_id IS NULL AND status = 'DISPONIBLE'";
                    using (var cmd = new SqlCommand(updateQuery, connection, transaction))
                    {
                        cmd.Parameters.Add(new SqlParameter("@rental_id", SqlDbType.Int) { Value = rentalId });
                        cmd.Parameters.Add(new SqlParameter("@locker_id", SqlDbType.Int) { Value = lockerId });
                        totalAffected += await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            return totalAffected;
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
