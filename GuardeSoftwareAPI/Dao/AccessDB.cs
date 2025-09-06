using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace GuardeSoftwareAPI.Dao
{
    public class AccessDB
    {
        private readonly string routeDB = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=GuardeSoftware;Integrated Security=True;TrustServerCertificate=True;";

        public SqlConnection GetConnection()
        {
            SqlConnection connection = new SqlConnection(routeDB);
            try
            {
                connection.Open();
                Console.WriteLine("successful connection with DB.");
                return connection;
            }
            catch (Exception exc)
            {

                Console.WriteLine($"Error: {exc.Message}");
                throw;
            }
        }

        //This method is useful for SELECT queries
        //Returns a DataTable with the results of the query
        public DataTable GetTable(string tableName, string consult, SqlParameter[] parameters = null)
        {
            using (SqlConnection connection = new SqlConnection(routeDB))
            {
                using (SqlCommand command = new SqlCommand(consult, connection))
                {
                    if (parameters != null)
                    {

                        command.Parameters.AddRange(parameters);
                    }

                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable table = new DataTable(tableName);
                    adapter.Fill(table);
                    return table;

                }
            }

        }

        //This method realizes commands that do not return results (INSERT, UPDATE, DELETE)
        //Returns the number of affected rows
        public int ExecuteCommand(string query, SqlParameter[] parameters)
        {
            using (SqlConnection connection = new SqlConnection(routeDB))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }
                    try
                    {
                        connection.Open();
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected;
                    }
                    catch (SqlException sqlEx)
                    {
                        throw new Exception($"Error executing the query: {sqlEx.Message}", sqlEx);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error executing the query", ex);
                    }
                }
            }
        }

        //New async methods
        //Actually, they are used for jobs with Quartz
        //This method realizes commands that do not return results (INSERT, UPDATE, DELETE)
        //Returns the number of affected rows
        public async Task<int> ExecuteCommandAsync(string query, SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(routeDB))
            {
                using (var command = new SqlCommand(query, connection))
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
            using (var connection = new SqlConnection(routeDB))
            {
                using (var command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    await connection.OpenAsync();
                    object result = await command.ExecuteScalarAsync();
                    return result;
                }
            }
        }

    }
}