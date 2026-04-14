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
                    w.name AS WarehouseName,
                    rsr.quantity,
                    rsr.m3,
                    rsr.comment
                FROM rental_space_requests rsr
                INNER JOIN rentals r ON rsr.rental_id = r.rental_id
                INNER JOIN warehouses w ON rsr.warehouse_id = w.warehouse_id
                WHERE r.client_id = @ClientId AND r.active = 1";

            SqlParameter[] parameters = [
                new("@ClientId", SqlDbType.Int) { Value = clientId }
            ];

            DataTable table = await _accessDB.GetTableAsync("SpaceRequests", query, parameters);

            foreach (DataRow row in table.Rows)
            {
                list.Add(new GetSpaceRequestDetailDto
                {
                    Warehouse = row["WarehouseName"]?.ToString() ?? "Desconocido",
                    Quantity = row["quantity"] != DBNull.Value ? Convert.ToInt32(row["quantity"]) : 0,
                    M3 = row["m3"] != DBNull.Value ? Convert.ToDecimal(row["m3"]) : 0m,
                    Comment = row["comment"] != DBNull.Value ? row["comment"].ToString() : null
                });
            }

            return list;
        }
    }
}