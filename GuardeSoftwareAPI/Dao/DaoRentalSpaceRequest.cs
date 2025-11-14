using GuardeSoftwareAPI.Entities;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using System;

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
    }
}