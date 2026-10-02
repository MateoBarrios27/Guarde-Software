using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using System;
using GuardeSoftwareAPI.Dtos.RentalSpaceRequest;

namespace GuardeSoftwareAPI.Dao
{
    public class DaoRentalSpaceRequest
    {
        private readonly AccessDB _accessDB;

        public DaoRentalSpaceRequest(AccessDB accessDB)
        {
            _accessDB = accessDB;
        }

        /// <summary>
        /// Inserta una solicitud de espacio dentro de una transacción existente.
        /// </summary>
        public async Task<int> CreateRequestTransactionAsync(RentalSpaceRequest request, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                INSERT INTO rental_space_requests (rental_id, warehouse_id, quantity, m3, comment)
                OUTPUT INSERTED.request_id
                VALUES (@RentalId, @WarehouseId, @Quantity, @M3, @Comment);";

            SqlParameter[] parameters =
            [
                new("@RentalId", SqlDbType.Int) { Value = request.RentalId },
                new("@WarehouseId", SqlDbType.Int) { Value = request.WarehouseId },
                new("@Quantity", SqlDbType.Int) { Value = request.Quantity },
                new("@M3", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = request.M3 },
                new("@Comment", SqlDbType.NVarChar, 500) { Value = (object)request.Comment ?? DBNull.Value }
            ];

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                object result = await command.ExecuteScalarAsync();
                
                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException("No se pudo crear el registro de solicitud de espacio.");

                return Convert.ToInt32(result);
            }
        }

        public async Task<List<GetSpaceRequestDetailDto>> GetRequestsByClientIdAsync(int clientId)
        {
            var list = new List<GetSpaceRequestDetailDto>();

            string query = @"
                SELECT 
                    rsr.request_id,
                    w.name AS WarehouseName,
                    rsr.quantity,
                    rsr.m3,
                    rsr.comment
                FROM rental_space_requests rsr
                INNER JOIN rentals r ON rsr.rental_id = r.rental_id
                INNER JOIN warehouses w ON rsr.warehouse_id = w.warehouse_id
                WHERE r.client_id = @ClientId
                  AND r.active = 1
                  AND rsr.removed_at IS NULL
                ORDER BY rsr.request_id";

            SqlParameter[] parameters = [
                new("@ClientId", SqlDbType.Int) { Value = clientId }
            ];

            DataTable table = await _accessDB.GetTableAsync("SpaceRequests", query, parameters);

            foreach (DataRow row in table.Rows)
            {
                list.Add(new GetSpaceRequestDetailDto
                {
                    Id = Convert.ToInt32(row["request_id"]),
                    Warehouse = row["WarehouseName"]?.ToString() ?? "Desconocido",
                    Quantity = row["quantity"] != DBNull.Value ? Convert.ToInt32(row["quantity"]) : 0,
                    M3 = row["m3"] != DBNull.Value ? Convert.ToDecimal(row["m3"]) : 0m,
                    Comment = row["comment"] != DBNull.Value ? row["comment"].ToString() : null
                });
            }

            return list;
        }

        public async Task<(int RentalId, DateTime RemovedAt)> RemoveRequestTransactionAsync(
            int clientId,
            int requestId,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            const string findQuery = @"
                SELECT rsr.rental_id
                FROM rental_space_requests rsr WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN rentals r ON rsr.rental_id = r.rental_id
                WHERE rsr.request_id = @RequestId
                  AND r.client_id = @ClientId
                  AND r.active = 1
                  AND rsr.removed_at IS NULL;";

            int? rentalId = null;
            using (var findCommand = new SqlCommand(findQuery, connection, transaction))
            {
                findCommand.Parameters.Add(new SqlParameter("@RequestId", SqlDbType.Int) { Value = requestId });
                findCommand.Parameters.Add(new SqlParameter("@ClientId", SqlDbType.Int) { Value = clientId });
                object? result = await findCommand.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    rentalId = Convert.ToInt32(result);
            }

            if (!rentalId.HasValue)
                throw new InvalidOperationException("No se encontró el espacio solicitado o no pertenece al alquiler activo del cliente.");

            const string removeQuery = @"
                UPDATE rental_space_requests
                SET removed_at = DATEADD(hour, -3, GETUTCDATE())
                OUTPUT INSERTED.removed_at
                WHERE request_id = @RequestId
                  AND rental_id = @RentalId
                  AND removed_at IS NULL;";

            DateTime? removedAt = null;
            using (var removeCommand = new SqlCommand(removeQuery, connection, transaction))
            {
                removeCommand.Parameters.Add(new SqlParameter("@RequestId", SqlDbType.Int) { Value = requestId });
                removeCommand.Parameters.Add(new SqlParameter("@RentalId", SqlDbType.Int) { Value = rentalId.Value });
                object? result = await removeCommand.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    removedAt = Convert.ToDateTime(result);

                if (!removedAt.HasValue)
                    throw new InvalidOperationException("No se pudo eliminar el espacio solicitado.");
            }

            return (rentalId.Value, removedAt.Value);
        }

        public async Task<decimal> CalculateRequestedM3TransactionAsync(
            int rentalId,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            const string query = @"
                SELECT ISNULL(SUM(m3 * quantity), 0)
                FROM rental_space_requests
                WHERE rental_id = @RentalId
                  AND removed_at IS NULL;";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@RentalId", SqlDbType.Int) { Value = rentalId });
            object? result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
        }
    }
}
