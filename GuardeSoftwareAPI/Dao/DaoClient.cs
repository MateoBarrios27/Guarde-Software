using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.Client;
using System.Text;


namespace GuardeSoftwareAPI.Dao
{

    public class DaoClient
    {
        private readonly AccessDB accessDB;

        public DaoClient(AccessDB _accessDB)
        {
            accessDB = _accessDB;
        }

        public async Task<DataTable> GetClients()
        {

            string query = "SELECT client_id, payment_identifier,first_name,last_name,registration_date,dni,cuit,preferred_payment_method_id,iva_condition, notes FROM clients WHERE active=1";

            return await accessDB.GetTableAsync("clients", query);
        }

        public async Task<DataTable> GetClientById(int id)
        {
            string query = "SELECT client_id, payment_identifier,first_name,last_name,registration_date,dni,cuit,preferred_payment_method_id,iva_condition, notes FROM clients WHERE client_id = @client_id";

            SqlParameter[] parameters = [

                new("@client_id",SqlDbType.Int) {Value = id},
            ];

            return await accessDB.GetTableAsync("clients", query, parameters);
        }

        public async Task<bool> CreateClient(Client client)
        {
            SqlParameter[] parameters = [

                new("@payment_identifier",SqlDbType.Decimal) {Value = client.PaymentIdentifier},
                new("@first_name",SqlDbType.VarChar) {Value = client.FirstName },
                new("@last_name",SqlDbType.VarChar) {Value = client.LastName},
                new("@registration_date",SqlDbType.DateTime) {Value = client.RegistrationDate},
                new("@dni",SqlDbType.VarChar) {Value = client.Dni},
                new("@cuit",SqlDbType.VarChar) {Value = client.Cuit},
                new("@preferred_payment_method_id",SqlDbType.Int) {Value = client.PreferredPaymentMethodId},
                new("@iva_condition",SqlDbType.VarChar) {Value = client.IvaCondition},
                new("@notes",SqlDbType.VarChar) {Value = client.Notes},
            ];

            string query = "INSERT INTO clients(payment_identifier,first_name,last_name,registration_date,dni,cuit,preferred_payment_method_id,iva_condition, notes)"
            + "VALUES(@payment_identifier,@first_name,@last_name,@registration_date,@dni,@cuit,@preferred_payment_method_id,@iva_condition, @notes)";

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }


        public async Task<bool> DeleteClientById(int id)
        {

            string query = "UPDATE clients SET active = 0 WHERE client_id = @client_id";

            SqlParameter[] parameters =
            [
                new("@client_id", SqlDbType.Int){Value = id},
            ];

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;

        }

        public async Task<int> CreateClientAsync(Client client)
        {

            SqlParameter[] parameters =
            [

                new("@payment_identifier", SqlDbType.Decimal)
                {
                    Precision = 10,
                    Scale = 2,
                    Value = (object?)client.PaymentIdentifier ?? DBNull.Value
                },
                new("@first_name", SqlDbType.VarChar) { Value = (object?)client.FirstName?.Trim() ?? DBNull.Value },
                new("@last_name", SqlDbType.VarChar) { Value = (object?)client.LastName?.Trim() ?? DBNull.Value },
                new("@registration_date", SqlDbType.DateTime) { Value = client.RegistrationDate },
                new("@dni", SqlDbType.VarChar) { Value = (object?)client.Dni?.Trim() ?? DBNull.Value },
                new("@cuit", SqlDbType.VarChar) { Value = (object?)client.Cuit?.Trim() ?? DBNull.Value },
                new("@preferred_payment_method_id", SqlDbType.Int)
                {
                    Value = client.PreferredPaymentMethodId > 0 ? (object)client.PreferredPaymentMethodId : DBNull.Value
                },
                new("@iva_condition", SqlDbType.VarChar) { Value = (object?)client.IvaCondition?.Trim() ?? DBNull.Value },
                new("@notes", SqlDbType.VarChar) { Value = (object?)client.Notes?.Trim() ?? DBNull.Value },
            ];

            // Important: OUTPUT INSERTED.id returns the id even though there are triggers. 
            // instead: Identity scope can return the wrong ID if there are triggers
            string query = @"
            INSERT INTO clients(payment_identifier, first_name, last_name, registration_date, dni, cuit, preferred_payment_method_id, iva_condition, notes)
            OUTPUT INSERTED.client_id
            VALUES(@payment_identifier, @first_name, @last_name, @registration_date, @dni, @cuit, @preferred_payment_method_id, @iva_condition, @notes);";

            object result = await accessDB.ExecuteScalarAsync(query, parameters);

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("The newly added customer id could not be returned.");

            return Convert.ToInt32(result);
        }


        //METHOD FOR TRANSACTION
        public async Task<int> CreateClientTransactionAsync(Client client, SqlConnection connection, SqlTransaction transaction)
        {
            SqlParameter[] parameters =
            [
                new("@payment_identifier", SqlDbType.Decimal)
                {
                    Precision = 10,
                    Scale = 2,
                    Value = (object?)client.PaymentIdentifier ?? DBNull.Value
                },
                new("@first_name", SqlDbType.VarChar) { Value = (object?)client.FirstName?.Trim() ?? DBNull.Value },
                new("@last_name", SqlDbType.VarChar) { Value = (object?)client.LastName?.Trim() ?? DBNull.Value },
                new("@registration_date", SqlDbType.DateTime) { Value = client.RegistrationDate },
                new("@dni", SqlDbType.VarChar) { Value = (object?)client.Dni?.Trim() ?? DBNull.Value },
                new("@cuit", SqlDbType.VarChar) { Value = (object?)client.Cuit?.Trim() ?? DBNull.Value },
                new("@preferred_payment_method_id", SqlDbType.Int)
                {
                    Value = client.PreferredPaymentMethodId > 0 ? (object)client.PreferredPaymentMethodId : DBNull.Value
                },
                new("@iva_condition", SqlDbType.VarChar) { Value = (object?)client.IvaCondition?.Trim() ?? DBNull.Value },
                new("@notes", SqlDbType.VarChar) { Value = (object?)client.Notes?.Trim() ?? DBNull.Value },
            ];

            string query = @"
                            INSERT INTO clients(payment_identifier, first_name, last_name, registration_date, dni, cuit, preferred_payment_method_id, iva_condition, notes)
                            OUTPUT INSERTED.client_id
                            VALUES(@payment_identifier, @first_name, @last_name, @registration_date, @dni, @cuit, @preferred_payment_method_id, @iva_condition, @notes);";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            object result = await command.ExecuteScalarAsync() ?? DBNull.Value;

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("The newly added customer id could not be returned.");

            return Convert.ToInt32(result);
        }

        //Here missing the method to get balance and payment status
        public async Task<DataTable> GetClientDetailByIdAsync(int id)
        {
            string query = @"
                WITH CurrentRentalAmount AS (
                    SELECT rental_id, amount as CurrentRent
                    FROM (
                        SELECT 
                            rental_id, 
                            amount, 
                            ROW_NUMBER() OVER(PARTITION BY rental_id ORDER BY start_date DESC) as rn
                        FROM rental_amount_history
                    ) as sub
                    WHERE rn = 1
                ),
                AccountSummary AS (
                    SELECT
                        rental_id,
                        SUM(CASE WHEN movement_type = 'DEBITO' THEN amount ELSE -amount END) AS Balance,
                        MAX(CASE WHEN movement_type = 'DEBITO' THEN movement_date END) AS LastDebitDate
                    FROM
                        account_movements
                    GROUP BY
                        rental_id
                )
                SELECT 
                    c.client_id, c.payment_identifier, c.first_name, c.last_name, c.registration_date,
                    c.dni, c.cuit, c.iva_condition, c.notes,
                    em.address AS email_address, 
                    ph.number AS phone_number, 
                    ad.street, ad.city, ad.province,
                    pm.name AS preferred_payment_method,
                    r.contracted_m3,
                    cir.end_date,
                    ir.frequency AS increase_frequency, 
                    ir.percentage AS increase_percentage,
                    cra.CurrentRent AS rent_amount,
                    ISNULL(acc.Balance, 0) AS balance,
                    CASE
                        WHEN ISNULL(acc.Balance, 0) > 0 OR acc.LastDebitDate IS NULL OR cra.CurrentRent IS NULL OR cra.CurrentRent = 0 THEN
                            CASE
                                WHEN DAY(GETDATE()) <= 10 THEN DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 10)
                                ELSE DATEADD(month, 1, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 10))
                            END
                        ELSE
                            DATEADD(month, 1 + FLOOR(-acc.Balance / cra.CurrentRent), acc.LastDebitDate)
                    END AS next_payment_day,
                    CASE 
                        WHEN ISNULL(acc.Balance, 0) <= 0 THEN 'Al día'
                        WHEN acc.Balance > cra.CurrentRent THEN 'Moroso'
                        WHEN DAY(GETDATE()) > 10 AND acc.Balance > 0 THEN 'Vencido'
                        WHEN acc.Balance > 0 THEN 'Pendiente'
                        ELSE 'Revisar'
                    END AS payment_status
                FROM 
                    clients c
                LEFT JOIN addresses ad ON c.client_id = ad.client_id
                LEFT JOIN emails em ON c.client_id = em.client_id AND em.active = 1
                LEFT JOIN phones ph ON c.client_id = ph.client_id AND ph.active = 1
                LEFT JOIN clients_x_increase_regimens cir ON c.client_id = cir.client_id
                LEFT JOIN increase_regimens ir ON cir.regimen_id = ir.regimen_id
                LEFT JOIN payment_methods pm ON c.preferred_payment_method_id = pm.payment_method_id
                LEFT JOIN rentals r ON c.client_id = r.client_id AND r.active = 1 
                LEFT JOIN AccountSummary acc ON r.rental_id = acc.rental_id
                LEFT JOIN CurrentRentalAmount cra ON r.rental_id = cra.rental_id
                WHERE c.client_id = @client_id AND c.active = 1;";

            SqlParameter[] parameters = [
                new SqlParameter("@client_id", SqlDbType.Int) { Value = id },
            ];

            return await accessDB.GetTableAsync("client_details", query, parameters);
        }

        public async Task<bool> ExistsByDniAsync(string dni, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM clients WHERE dni = @dni";
            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@dni", SqlDbType.VarChar) { Value = dni });
                object result = await command.ExecuteScalarAsync();
                int count = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
                return count > 0;
            }
        }

        public async Task<bool> ExistsByCuitAsync(string cuit, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM clients WHERE cuit = @cuit";
            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@cuit", SqlDbType.VarChar) { Value = cuit });
                int count = (int)await command.ExecuteScalarAsync();
                return count > 0;
            }
        }

        //This method returns a tuple with the list of clients and the total count for pagination
        //Not returns a datatable like the others because we need to map it to a dto, the method is
        //below this (MapDataTableToDto)
        
        //Sure this method needs a improvement to calculate the balance and payment status
        public async Task<(List<GetTableClientsDto> clients, int totalCount)> GetTableClientsAsync(GetClientsRequestDto request)
        {
            var filterParameters = new List<SqlParameter>();
            var whereClause = new StringBuilder("WHERE 1=1 ");

            // --- LÓGICA DE FILTROS ---
            if (request.Active.HasValue)
            {
                whereClause.Append("AND Active = @Active ");
                filterParameters.Add(new SqlParameter("@Active", request.Active.Value));
            }

            // Filtro del buscador potente
            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                whereClause.Append(@"
                    AND (
                        ISNULL(FirstName, '') + ' ' + ISNULL(LastName, '') LIKE @SearchTerm OR
                        ISNULL(Email, '') LIKE @SearchTerm OR
                        ISNULL(Document, '') LIKE @SearchTerm OR
                        CAST(PaymentIdentifier AS NVARCHAR(50)) LIKE @SearchTerm
                    ) ");
                filterParameters.Add(new SqlParameter("@SearchTerm", $"%{request.SearchTerm}%"));
            }

            // Filtro de estado (Moroso, Al día, etc.)
            if (!string.IsNullOrEmpty(request.StatusFilter) && request.StatusFilter != "Todos")
            {
                whereClause.Append("AND Status = @StatusFilter ");
                filterParameters.Add(new SqlParameter("@StatusFilter", request.StatusFilter));
            }

            // --- CONSTRUCCIÓN DE LA QUERY CON CTE ---
            string fullQuery = $@"
                WITH ClientData AS (
                    SELECT
                        c.client_id AS Id,
                        c.payment_identifier AS PaymentIdentifier,
                        c.first_name AS FirstName,
                        c.last_name AS LastName,
                        first_email.address AS Email,
                        first_phone.number AS Phone,
                        a.city AS City,
                        ISNULL(balance_sub.balance, 0) as Balance,
                        c.preferred_payment_method_id AS PreferredPaymentMethodId,
                        c.dni AS Document, 
                        locker_sub.lockers as Lockers,
                        c.active AS Active,
                        CASE
                            WHEN c.active = 0 THEN 'Baja'
                            WHEN ISNULL(months_unpaid_sub.total_months_unpaid, 0) >= 1 THEN 'Moroso'
                            WHEN ISNULL(balance_sub.balance, 0) <= 0 THEN 'Al día'
                            ELSE 'Pendiente'
                        END AS Status
                    FROM 
                        clients c
                    OUTER APPLY ( SELECT TOP 1 e.address FROM emails e WHERE e.client_id = c.client_id AND e.active = 1 ORDER BY e.email_id ) AS first_email
                    OUTER APPLY ( SELECT TOP 1 p.number FROM phones p WHERE p.client_id = c.client_id AND p.active = 1 ORDER BY p.phone_id ) AS first_phone
                    LEFT JOIN addresses a ON c.client_id = a.client_id
                    LEFT JOIN ( SELECT r.client_id, SUM(am.amount * CASE WHEN am.movement_type = 'DEBITO' THEN 1 ELSE -1 END) as balance FROM rentals r JOIN account_movements am ON r.rental_id = am.rental_id GROUP BY r.client_id ) balance_sub ON c.client_id = balance_sub.client_id
                    LEFT JOIN ( SELECT r.client_id, STRING_AGG(l.identifier, ', ') as lockers FROM rentals r JOIN lockers l ON r.rental_id = l.rental_id GROUP BY r.client_id ) locker_sub ON c.client_id = locker_sub.client_id
                    LEFT JOIN ( SELECT r.client_id, SUM(ISNULL(r.months_unpaid, 0)) as total_months_unpaid FROM rentals r WHERE r.active = 1 GROUP BY r.client_id ) months_unpaid_sub ON c.client_id = months_unpaid_sub.client_id
                ),
                FilteredCount AS (
                    SELECT COUNT(*) AS TotalRows FROM ClientData {whereClause}
                )
                SELECT * FROM ClientData, FilteredCount
                {whereClause}
                ORDER BY {GetSortColumn(request.SortField)} {GetSortDirection(request.SortDirection)}
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            ";

            var dataParameters = new List<SqlParameter>();
            dataParameters.AddRange(filterParameters.Select(p => new SqlParameter(p.ParameterName, p.Value)));
            dataParameters.Add(new SqlParameter("@Offset", (request.PageNumber - 1) * request.PageSize));
            dataParameters.Add(new SqlParameter("@PageSize", request.PageSize));

            DataTable dataTable = await accessDB.GetTableAsync("Clients", fullQuery, dataParameters.ToArray());
            
            int totalCount = 0;
            if (dataTable.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(dataTable.Rows[0]["TotalRows"]);
            }
            
            var clients = MapDataTableToDto(dataTable);

            return (clients, totalCount);
        }

        // Helper methods to avoid SQL injection
       // En tu método GetTableClientsAsync del DAO

        private string GetSortColumn(string sortField)
        {
            // Usamos un diccionario que ignora mayúsculas/minúsculas
            var validSortFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) 
            {
                // "Clave del Front-end", "Nombre de la Columna en la CTE"
                { "FirstName", "FirstName" },
                { "LastName", "LastName" },
                { "Baulera", "Lockers" },
                { "Estado", "Status" },
                { "Balance", "Balance" } // "Renta" en la imagen, "Balance" en el código
            };
            
            // Si el campo existe en el diccionario, lo devuelve. Si no, devuelve "Id" por defecto.
            return validSortFields.TryGetValue(sortField, out var dbColumn) ? dbColumn : "Id";
        }

        private string GetSortDirection(string sortDirection)
        {
            return sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        }

        private List<GetTableClientsDto> MapDataTableToDto(DataTable table)
        {
            var clients = new List<GetTableClientsDto>();
            if (table == null) return clients;

            foreach (DataRow row in table.Rows)
            {
                clients.Add(new GetTableClientsDto
                {
                    Id = Convert.ToInt32(row["Id"]),
                    PaymentIdentifier = row["PaymentIdentifier"] != DBNull.Value ? Convert.ToDecimal(row["PaymentIdentifier"]) : null,
                    FirstName = row["FirstName"]?.ToString() ?? string.Empty,
                    LastName = row["LastName"]?.ToString() ?? string.Empty,
                    Email = row["Email"]?.ToString(),
                    Phone = row["Phone"]?.ToString(),
                    City = row["City"]?.ToString() ?? string.Empty,
                    Balance = Convert.ToDecimal(row["Balance"]),
                    Status = row["Status"]?.ToString() ?? "Pendiente",
                    PreferredPaymentMethodId = row["PreferredPaymentMethodId"] != DBNull.Value ? Convert.ToInt32(row["PreferredPaymentMethodId"]) : null,
                    Document = row["Document"]?.ToString(),
                    Lockers = row["Lockers"] != DBNull.Value ? row["Lockers"].ToString()!.Split(',').ToList() : null,
                    Active = Convert.ToBoolean(row["Active"])
                });
            }
            return clients;
        }

        public async Task<List<string>> GetActiveClientNamesAsync()
        {
            var names = new List<string>();
            string query = "SELECT first_name, last_name FROM clients WHERE active = 1 ORDER BY first_name, last_name";

            DataTable table = await accessDB.GetTableAsync("ClientNames", query);

            foreach (DataRow row in table.Rows)
            {
                names.Add($"{row["first_name"]} {row["last_name"]}");
            }
            return names;
        }
        
        public async Task<List<string>> SearchActiveClientNamesAsync(string query)
        {
            var names = new List<string>();
            
            // Buscamos coincidencias en nombre, apellido o nombre completo
            string sqlQuery = @"
                SELECT first_name, last_name 
                FROM clients 
                WHERE active = 1 AND 
                    (first_name LIKE @Query OR 
                    last_name LIKE @Query OR 
                    (first_name + ' ' + last_name) LIKE @Query)
                ORDER BY first_name, last_name
                OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY; -- Limitamos a 10 resultados
            ";
            
            var parameters = new[] { 
                // Añadimos '%' para que funcione como un 'CONTAINS'
                new SqlParameter("@Query", $"%{query}%") 
            };

            DataTable table = await accessDB.GetTableAsync("ClientSearch", sqlQuery, parameters);

            foreach (DataRow row in table.Rows)
            {
                names.Add($"{row["first_name"]} {row["last_name"]}");
            }
            return names;
        }
    }
}

