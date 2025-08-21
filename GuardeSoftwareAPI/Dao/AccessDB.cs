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
    }
}