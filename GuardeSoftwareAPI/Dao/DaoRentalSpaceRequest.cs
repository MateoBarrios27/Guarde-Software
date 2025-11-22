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
                INSERT INTO rental_space_requests (rental_id, warehouse_id, quantity, m3)
                OUTPUT INSERTED.request_id
                VALUES (@RentalId, @WarehouseId, @Quantity, @M3);";

            SqlParameter[] parameters =
            {
                new SqlParameter("@RentalId", SqlDbType.Int) { Value = request.RentalId },
                new SqlParameter("@WarehouseId", SqlDbType.Int) { Value = request.WarehouseId },
                new SqlParameter("@Quantity", SqlDbType.Int) { Value = request.Quantity },
                new SqlParameter("@M3", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = request.M3 }
            };

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

            // Unimos rental_space_requests -> rentals -> warehouses para obtener el nombre y filtrar por cliente
            string query = @"
                SELECT 
                    w.name AS WarehouseName,
                    rsr.quantity,
                    rsr.m3
                FROM rental_space_requests rsr
                INNER JOIN rentals r ON rsr.rental_id = r.rental_id
                INNER JOIN warehouses w ON rsr.warehouse_id = w.warehouse_id
                WHERE r.client_id = @ClientId AND r.active = 1";

            SqlParameter[] parameters = {
                new SqlParameter("@ClientId", SqlDbType.Int) { Value = clientId }
            };

            DataTable table = await _accessDB.GetTableAsync("SpaceRequests", query, parameters);

            foreach (DataRow row in table.Rows)
            {
                list.Add(new GetSpaceRequestDetailDto
                {
                    Warehouse = row["WarehouseName"]?.ToString() ?? "Desconocido",
                    Quantity = row["quantity"] != DBNull.Value ? Convert.ToInt32(row["quantity"]) : 0,
                    M3 = row["m3"] != DBNull.Value ? Convert.ToDecimal(row["m3"]) : 0m
                });
            }

            return list;
        }
    }
}