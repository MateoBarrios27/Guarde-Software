using System;
using System.Data;
using Microsoft.Data.SqlClient;


namespace GuardeSoftwareAPI.Dao
{
    public class AccessDB
    {
        private readonly string routeDB = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=GuardeSoftware;Integrated Security=True;TrustServerCertificate=True;";

        public SqlConnection GetConnectionClose()
        {
            return new SqlConnection(routeDB);
        }
        //This method realizes commands that do not return results (INSERT, UPDATE, DELETE)
        //Returns the number of affected rows
        public async Task<int> ExecuteCommandAsync(string query, SqlParameter[] parameters)
        {
            using (SqlConnection connection = new SqlConnection(routeDB))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected;
                }
            }
        }

        //This method is useful for getting the last inserted id
        //Returns an object that can be cast to the desired type
        //Example of use:
        //string query = "INSERT INTO table_name(column1, column2) VALUES(@value1, @value2); SELECT SCOPE_IDENTITY();";
        //object result = await accessDB.ExecuteScalarAsync(query, parameters);
        public async Task<object> ExecuteScalarAsync(string query, SqlParameter[] parameters = null)
        {
            using (SqlConnection connection = new SqlConnection(routeDB))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    await connection.OpenAsync();
                    object result = await command.ExecuteScalarAsync() ?? DBNull.Value;
                    return result;
                }
            }
        }

        //This method is useful for SELECT queries
        //Returns a DataTable with the results of the query
        //It'a an async version of GetTable, it is used in DaoRental to realize jobs with Quartz
        public async Task<DataTable> GetTableAsync(string tableName, string query, SqlParameter[] parameters = null)
        {
            using (SqlConnection connection = new SqlConnection(routeDB))
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    DataTable table = new DataTable(tableName);
                    table.Load(reader); // Load the DataTable from the SqlDataReader
                    return table;
                }
            }
        }


    }
}