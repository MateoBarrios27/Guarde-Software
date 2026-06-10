using System;
using System.Data;
using Microsoft.Data.SqlClient;
using GuardeSoftwareAPI.Entities;
using System.Threading.Tasks;
using GuardeSoftwareAPI.Dtos.Client;
using System.Text;
using System.Text.Json;


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
            string query = @"
                WITH CurrentRentalAmount AS (
                    SELECT 
                        h.rental_id,
                        h.amount AS CurrentRent
                    FROM (
                        SELECT 
                            rental_id, 
                            amount,
                            ROW_NUMBER() OVER (
                                PARTITION BY rental_id 
                                ORDER BY start_date DESC, CASE WHEN end_date IS NULL THEN 1 ELSE 0 END DESC, rental_amount_history_id DESC
                            ) as rn
                        FROM rental_amount_history
                        WHERE start_date <= GETDATE()
                    ) h
                    WHERE h.rn = 1
                )
                SELECT
                    c.client_id,
                    c.payment_identifier,
                    c.full_name,
                    c.registration_date,
                    c.dni,
                    c.cuit,
                    c.preferred_payment_method_id,
                    c.iva_condition,
                    c.notes,
                    c.billing_type_id,
                    c.increase_frequency_months,
                    c.initial_amount,
                    r.increase_anchor_date AS IncreaseAnchorDate,
                    r.pending_surcharge AS PendingSurcharge,
                    ISNULL(step1.UI_CurrentRent, ISNULL(cr.CurrentRent, 0)) AS rent_amount,
                    ISNULL(step1.UI_Balance, 0) AS balance,
                    ISNULL(step1.UI_InterestAmount, 0) AS interest_amount,
                    ISNULL(db.MonthYearDB, '') AS last_generated_month_year
                FROM clients c
                LEFT JOIN rentals r
                    ON c.client_id = r.client_id
                    AND r.active = 1
                LEFT JOIN CurrentRentalAmount cr
                    ON r.rental_id = cr.rental_id
                
                -- BÚSQUEDA INTELIGENTE DEL MES ACTIVO (Siempre la última tabla generada)
                OUTER APPLY (
                    SELECT TOP 1
                        PrevBalDB = ISNULL(cmb.previous_balance, 0),
                        IntsDB = ISNULL(cmb.interests, 0),
                        RentDB = CASE WHEN ISNULL(cmb.monthly_debits, 0) = 0 THEN ISNULL(cr.CurrentRent, 0) ELSE cmb.monthly_debits END,
                        PaidDB = ISNULL(cmb.paid, 0),
                        AdvPayDB = ISNULL(cmb.advanced_payment, 0),
                        MonthYearDB = cmb.month_year -- <-- 2. LO EXTRAEMOS DE LA DB
                    FROM client_month_balances cmb
                    WHERE cmb.rental_id = r.rental_id
                      AND (cmb.balance - cmb.paid - cmb.advanced_payment) > 0
                    ORDER BY cmb.id DESC
                ) db
                
                -- CÁLCULO SEGURO DEL BALANCE MATEMÁTICO
                OUTER APPLY (
                    SELECT
                        UI_CurrentRent = db.RentDB,
                        UI_InterestAmount = ISNULL((
                            SELECT SUM(ISNULL(cmb2.interests, 0))
                            FROM client_month_balances cmb2
                            WHERE cmb2.rental_id = r.rental_id
                              AND (cmb2.balance - cmb2.paid - cmb2.advanced_payment) > 0
                        ), 0),
                        -- Proyección absoluta: (Arrastre + Interés + Cuota) - (Lo pagado + Lo adelantado)
                        UI_Balance = -(db.PrevBalDB + db.IntsDB + db.RentDB - db.PaidDB - db.AdvPayDB)
                ) step1
                
                WHERE c.active = 1;";   

            return await accessDB.GetTableAsync("clients", query);
        }

        public async Task<DataTable> GetClientById(int id)
        {
            string query = "SELECT client_id, payment_identifier,full_name,registration_date,dni,cuit,preferred_payment_method_id,iva_condition, notes, billing_type_id, increase_frequency_months, initial_amount FROM clients WHERE client_id = @client_id";
            SqlParameter[] parameters = { new("@client_id", SqlDbType.Int) { Value = id } };
            return await accessDB.GetTableAsync("clients", query, parameters);
        }

        public async Task<bool> CreateClient(Client client)
        {
            SqlParameter[] parameters = [

                new("@payment_identifier",SqlDbType.Decimal) {Value = client.PaymentIdentifier},
                new("@full_name",SqlDbType.VarChar) {Value = client.FullName },
                new("@registration_date",SqlDbType.DateTime) {Value = client.RegistrationDate},
                new("@dni",SqlDbType.VarChar) {Value = client.Dni},
                new("@cuit",SqlDbType.VarChar) {Value = client.Cuit},
                new("@preferred_payment_method_id",SqlDbType.Int) {Value = client.PreferredPaymentMethodId},
                new("@iva_condition",SqlDbType.VarChar) {Value = client.IvaCondition},
                new("@notes",SqlDbType.VarChar) {Value = client.Notes},
            ];

            string query = "INSERT INTO clients(payment_identifier,full_name,registration_date,dni,cuit,preferred_payment_method_id,iva_condition, notes)"
            + "VALUES(@payment_identifier,@full_name,@registration_date,@dni,@cuit,@preferred_payment_method_id,@iva_condition, @notes)";

            return await accessDB.ExecuteCommandAsync(query, parameters) > 0;
        }


        public async Task<bool> DeactivateClientTransactionAsync(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "UPDATE clients SET active = 0 WHERE client_id = @Id";
            SqlParameter[] parameters = [new SqlParameter("@Id", SqlDbType.Int) { Value = clientId }];

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            int rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
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
                new("@full_name", SqlDbType.VarChar) { Value = (object?)client.FullName?.Trim() ?? DBNull.Value },
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
            INSERT INTO clients(payment_identifier, full_name, registration_date, dni, cuit, preferred_payment_method_id, iva_condition, notes)
            OUTPUT INSERTED.client_id
            VALUES(@payment_identifier, @full_name, @registration_date, @dni, @cuit, @preferred_payment_method_id, @iva_condition, @notes);";

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
                new("@payment_identifier", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = (object?)client.PaymentIdentifier ?? DBNull.Value },
                new("@full_name", SqlDbType.VarChar) { Value = (object?)client.FullName?.Trim() ?? DBNull.Value },
                new("@registration_date", SqlDbType.DateTime) { Value = client.RegistrationDate },
                new("@dni", SqlDbType.VarChar) { Value = (object?)client.Dni?.Trim() ?? DBNull.Value },
                new("@cuit", SqlDbType.VarChar) { Value = (object?)client.Cuit?.Trim() ?? DBNull.Value },
                new("@preferred_payment_method_id", SqlDbType.Int) { Value = client.PreferredPaymentMethodId > 0 ? (object)client.PreferredPaymentMethodId : DBNull.Value },
                new("@iva_condition", SqlDbType.VarChar) { Value = (object?)client.IvaCondition?.Trim() ?? DBNull.Value },
                new("@notes", SqlDbType.VarChar) { Value = (object?)client.Notes?.Trim() ?? DBNull.Value },
                new("@billing_type_id", SqlDbType.Int) { Value = (object?)client.BillingTypeId ?? DBNull.Value },
                new("@increase_frequency_months", SqlDbType.Int) { Value = client.IncreaseFrequencyMonths },
                new("@initial_amount", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = (object?)client.InitialAmount ?? DBNull.Value },
                new("@receive_communications", SqlDbType.Bit) { Value = client.ReceiveCommunications }
            ];

            string query = @"
                INSERT INTO clients(payment_identifier, full_name, registration_date, dni, cuit, preferred_payment_method_id, iva_condition, notes, billing_type_id, increase_frequency_months, initial_amount, receive_communications)
                OUTPUT INSERTED.client_id
                VALUES(@payment_identifier, @full_name, @registration_date, @dni, @cuit, @preferred_payment_method_id, @iva_condition, @notes, @billing_type_id, @increase_frequency_months, @initial_amount, @receive_communications);";

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            object result = await command.ExecuteScalarAsync() ?? DBNull.Value;

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("The newly added customer id could not be returned.");

            return Convert.ToInt32(result);
        }

        public async Task<DataTable> GetClientDetailByIdAsync(int id)
        {
            string query = @"
                WITH CurrentRentalAmount AS (
                    SELECT h.rental_id, h.amount AS CurrentRent
                    FROM (
                        SELECT rental_id, amount, ROW_NUMBER() OVER (PARTITION BY rental_id ORDER BY start_date DESC, CASE WHEN end_date IS NULL THEN 1 ELSE 0 END DESC, rental_amount_history_id DESC) as rn
                        FROM rental_amount_history WHERE start_date <= GETDATE()
                    ) h WHERE h.rn = 1
                )
                SELECT 
                    c.client_id, c.payment_identifier, c.full_name, c.registration_date,
                    c.dni, c.cuit, c.iva_condition, c.notes, c.receive_communications,
                    c.initial_amount, c.increase_frequency_months,
                    ad.street, ad.city, ad.province,
                    pm.name AS preferred_payment_method,
                    bt.billing_type_id, bt.name AS billing_type,
                    r.contracted_m3, r.increase_anchor_date, r.months_unpaid, r.occupied_spaces,
                    
                    -- BLINDAJE CONTRA NULOS Y PROYECCIÓN
                    ISNULL(step1.UI_CurrentRent, ISNULL(cr.CurrentRent, 0)) AS rent_amount,
                    ISNULL(step1.UI_InterestAmount, 0) AS interest_amount,
                    ISNULL(step1.UI_Balance, 0) AS balance,
                    ISNULL(db.PaidDB, 0) AS total_paid,
                    
                    -- LÓGICA DE PRÓXIMO PAGO BASADA EN MONTH_BALANCES
                    CASE 
                        WHEN step1.LastBalanceDate IS NULL OR step1.LastBalanceDate < CAST(GETDATE() AS DATE)
                        THEN DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 10)
                        ELSE step1.LastBalanceDate
                    END AS next_payment_day,

                    CASE 
                        WHEN c.active = 0 THEN 'Baja'
                        WHEN ISNULL(r.months_unpaid, 0) >= 1 THEN 'Moroso Nivel ' + CAST(ISNULL(r.months_unpaid, 0) AS VARCHAR(10))
                        WHEN ISNULL(step1.UI_Balance, 0) >= 0 THEN 'Al día'
                        ELSE 'Pendiente'
                    END AS payment_status
                    
                FROM clients c
                LEFT JOIN addresses ad ON c.client_id = ad.client_id
                LEFT JOIN payment_methods pm ON c.preferred_payment_method_id = pm.payment_method_id
                LEFT JOIN billing_types bt ON c.billing_type_id = bt.billing_type_id
                LEFT JOIN rentals r ON c.client_id = r.client_id AND r.active = 1 
                LEFT JOIN CurrentRentalAmount cr ON r.rental_id = cr.rental_id 

                -- BUSCAMOS EL MES ACTIVO (Siempre la última tabla generada)
                OUTER APPLY (
                    SELECT TOP 1
                        PrevBalDB = ISNULL(cmb.previous_balance, 0),
                        IntsDB = ISNULL(cmb.interests, 0),
                        RentDB = CASE WHEN ISNULL(cmb.monthly_debits, 0) = 0 THEN ISNULL(cr.CurrentRent, 0) ELSE cmb.monthly_debits END,
                        PaidDB = ISNULL(cmb.paid, 0),
                        AdvPayDB = ISNULL(cmb.advanced_payment, 0),
                        MonthYearDB = cmb.month_year -- Traemos el MM/yyyy del último balance
                    FROM client_month_balances cmb
                    WHERE cmb.rental_id = r.rental_id
                      AND (cmb.balance - cmb.paid - cmb.advanced_payment) > 0
                    ORDER BY cmb.id DESC -- LA SOLUCIÓN: Trae siempre la última tabla
                ) db

                -- CÁLCULO SEGURO DEL BALANCE MATEMÁTICO Y CONVERSIÓN DE FECHA
                OUTER APPLY (
                    SELECT 
                        UI_CurrentRent = db.RentDB, 
                        UI_InterestAmount = ISNULL((
                            SELECT SUM(ISNULL(cmb2.interests, 0))
                            FROM client_month_balances cmb2
                            WHERE cmb2.rental_id = r.rental_id
                              AND (cmb2.balance - cmb2.paid - cmb2.advanced_payment) > 0
                        ), 0),
                        UI_Balance = -(db.PrevBalDB + db.IntsDB + db.RentDB - db.PaidDB - db.AdvPayDB),
                        LastBalanceDate = CASE 
                            WHEN db.MonthYearDB IS NOT NULL AND LEN(db.MonthYearDB) = 7
                            THEN DATEFROMPARTS(CAST(RIGHT(db.MonthYearDB, 4) AS INT), CAST(LEFT(db.MonthYearDB, 2) AS INT), 10)
                            ELSE NULL 
                        END
                ) step1

                WHERE c.client_id = @client_id;";

            SqlParameter[] parameters = [ new("@client_id", SqlDbType.Int) { Value = id } ];
            return await accessDB.GetTableAsync("client_details", query, parameters);
        }

        public async Task<bool> ExistsByDniAsync(string dni, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM clients WHERE dni = @dni";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@dni", SqlDbType.VarChar) { Value = dni });
            object result = await command.ExecuteScalarAsync();
            int count = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
            return count > 0;
        }

        public async Task<bool> ExistsByCuitAsync(string cuit, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM clients WHERE cuit = @cuit";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@cuit", SqlDbType.VarChar) { Value = cuit });
            int count = (int)await command.ExecuteScalarAsync();
            return count > 0;
        }

        public async Task<bool> ExistsByPaymentIdentifierAsync(decimal paymentIdentifier, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM clients WHERE payment_identifier = @paymentIdentifier";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@paymentIdentifier", SqlDbType.Decimal) { Value = paymentIdentifier });
            int count = (int)await command.ExecuteScalarAsync();
            return count > 0;
        }

        public async Task<bool> ExistsByFullNameAsync(string fullName, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM clients WHERE full_name = @fullName AND active = 1";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@fullName", SqlDbType.VarChar) { Value = fullName });
            int count = (int)await command.ExecuteScalarAsync();
            return count > 0;
        }

        //This method returns a tuple with the list of clients and the total count for pagination
        //Not returns a datatable like the others because we need to map it to a dto, the method is
        //below this (MapDataTableToDto)
        
        //Sure this method needs a improvement to calculate the balance and payment status
        public async Task<(List<GetTableClientsDto> clients, int totalCount)> GetTableClientsAsync(GetClientsRequestDto request)
        {
            var filterParameters = new List<SqlParameter>();
            var finalWhereClause = new StringBuilder("WHERE 1=1 ");

            if (request.Active.HasValue)
            {
                finalWhereClause.Append("AND Active = @Active ");
                filterParameters.Add(new SqlParameter("@Active", request.Active.Value));
            }

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                finalWhereClause.Append(@"
                    AND (
                        ISNULL(FullName, '') LIKE @SearchTerm OR
                        ISNULL(Email, '') LIKE @SearchTerm OR
                        ISNULL(Document, '') LIKE @SearchTerm OR
                        CAST(PaymentIdentifier AS NVARCHAR(50)) LIKE @SearchTerm
                    ) ");
                filterParameters.Add(new SqlParameter("@SearchTerm", $"%{request.SearchTerm}%"));
            }

            if (!string.IsNullOrEmpty(request.StatusFilter) && request.StatusFilter != "Todos")
            {
                finalWhereClause.Append("AND Status = @StatusFilter ");
                filterParameters.Add(new SqlParameter("@StatusFilter", request.StatusFilter));
            }

            if (request.WarehouseId.HasValue && request.WarehouseId.Value > 0)
            {
                finalWhereClause.Append(@"
                    AND Id IN (
                        SELECT r.client_id
                        FROM rentals r
                        JOIN lockers l ON r.rental_id = l.rental_id
                        WHERE l.warehouse_id = @WarehouseId
                        AND r.active = 1
                    ) ");
                filterParameters.Add(new SqlParameter("@WarehouseId", request.WarehouseId.Value));
            }

            string fullQuery = $@"
                WITH CurrentRentalAmount AS (
                    SELECT h.rental_id, h.amount AS CurrentRent
                    FROM (
                        SELECT rental_id, amount,
                               ROW_NUMBER() OVER (PARTITION BY rental_id ORDER BY start_date DESC, CASE WHEN end_date IS NULL THEN 1 ELSE 0 END DESC, rental_amount_history_id DESC) as rn
                        FROM rental_amount_history WHERE start_date <= GETDATE()
                    ) h WHERE h.rn = 1
                ),
                ClientData AS (
                    SELECT
                        c.client_id AS Id,
                        c.payment_identifier AS PaymentIdentifier,
                        c.full_name AS FullName,
                        first_email.address AS Email,
                        first_phone.number AS Phone,
                        a.city AS City,

                        -- MATEMÁTICA DE LA UI 
                        step1.UI_PreviousBalance AS PreviousBalance,
                        step1.UI_InterestAmount AS InterestAmount,
                        ISNULL(step1.UI_CurrentRent, ISNULL(cr.CurrentRent, 0)) AS CurrentRent,
                        step1.UI_Balance AS Balance,

                        c.preferred_payment_method_id AS PreferredPaymentMethodId,
                        c.dni AS Document,
                        locker_sub.lockers as Lockers,
                        locker_sub.lockers_json as WarehouseLockersJson,
                        c.active AS Active,

                        CASE 
                            WHEN step1.LastBalanceDate IS NULL OR step1.LastBalanceDate < CAST(GETDATE() AS DATE)
                            THEN DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1) -- Día forzado a 1
                            ELSE step1.LastBalanceDate
                        END AS NextPaymentDay,
                        
                        CASE
                            WHEN c.active = 0 THEN 'Baja'
                            WHEN ISNULL(months_unpaid_sub.total_months_unpaid, 0) >= 1 THEN 'Moroso'
                            -- Como el saldo a pagar es negativo (ej: -110.000), si es >= 0 significa que no debe nada
                            WHEN ISNULL(step1.UI_Balance, 0) >= 0 THEN 'Al día' 
                            ELSE 'Pendiente'
                        END AS Status
                        
                    FROM clients c
                    OUTER APPLY ( SELECT TOP 1 e.address FROM emails e WHERE e.client_id = c.client_id AND e.active = 1 ORDER BY e.email_id ) AS first_email
                    OUTER APPLY ( SELECT TOP 1 p.number FROM phones p WHERE p.client_id = c.client_id AND p.active = 1 ORDER BY p.phone_id ) AS first_phone
                    LEFT JOIN addresses a ON c.client_id = a.client_id
                    LEFT JOIN rentals r ON c.client_id = r.client_id AND r.active = 1
                    LEFT JOIN CurrentRentalAmount cr ON r.rental_id = cr.rental_id

                    -- 1. BUSCAMOS EL MES ACTIVO (Siempre la última tabla generada)
                    OUTER APPLY (
                        SELECT TOP 1
                            Id = cmb.id,
                            PrevBalDB = ISNULL(cmb.previous_balance, 0),
                            IntsDB = ISNULL(cmb.interests, 0),
                            RentDB = CASE WHEN ISNULL(cmb.monthly_debits, 0) = 0 THEN ISNULL(cr.CurrentRent, 0) ELSE cmb.monthly_debits END,
                            PaidDB = ISNULL(cmb.paid, 0),
                            AdvPayDB = ISNULL(cmb.advanced_payment, 0),
                            MonthYearDB = cmb.month_year -- <--- NUEVO
                        FROM client_month_balances cmb
                        WHERE cmb.rental_id = r.rental_id
                          AND (cmb.balance - cmb.paid - cmb.advanced_payment) > 0
                        ORDER BY cmb.id DESC
                    ) db

                    -- 2. ASIGNAMOS A LA UI (Agregamos LastBalanceDate)
                    OUTER APPLY (
                        SELECT 
                            UI_CurrentRent = db.RentDB, 
                            UI_InterestAmount = ISNULL((
                                SELECT SUM(ISNULL(cmb2.interests, 0))
                                FROM client_month_balances cmb2
                                WHERE cmb2.rental_id = r.rental_id
                                  AND (cmb2.balance - cmb2.paid - cmb2.advanced_payment) > 0
                            ), 0),
                            UI_Balance = -(db.PrevBalDB + db.IntsDB + db.RentDB - db.PaidDB - db.AdvPayDB),
                            UI_PreviousBalance = -ISNULL((
                                SELECT SUM(
                                    CASE
                                        WHEN ISNULL(cmb2.monthly_debits, 0) - ISNULL(cmb2.paid, 0) - ISNULL(cmb2.advanced_payment, 0) > 0
                                        THEN ISNULL(cmb2.monthly_debits, 0) - ISNULL(cmb2.paid, 0) - ISNULL(cmb2.advanced_payment, 0)
                                        ELSE 0
                                    END
                                )
                                FROM client_month_balances cmb2
                                WHERE cmb2.rental_id = r.rental_id
                                  AND cmb2.id < db.id
                            ), 0),
                            
                            LastBalanceDate = CASE 
                                WHEN db.MonthYearDB IS NOT NULL AND LEN(db.MonthYearDB) = 7
                                THEN DATEFROMPARTS(CAST(RIGHT(db.MonthYearDB, 4) AS INT), CAST(LEFT(db.MonthYearDB, 2) AS INT), 1) -- Día forzado a 1
                                ELSE NULL 
                            END
                    ) step1

                    LEFT JOIN ( SELECT r.client_id, STRING_AGG(l.identifier, ', ') as lockers, ( SELECT ISNULL(w.name, 'Sin ubicación') AS Warehouse, STRING_AGG(l2.identifier, ', ') AS Lockers FROM rentals r2 JOIN lockers l2 ON r2.rental_id = l2.rental_id LEFT JOIN warehouses w ON l2.warehouse_id = w.warehouse_id WHERE r2.client_id = r.client_id AND r2.active = 1 GROUP BY w.name FOR JSON PATH ) as lockers_json FROM rentals r JOIN lockers l ON r.rental_id = l.rental_id WHERE r.active = 1 GROUP BY r.client_id ) locker_sub ON c.client_id = locker_sub.client_id
                    LEFT JOIN ( SELECT r.client_id, SUM(ISNULL(r.months_unpaid, 0)) as total_months_unpaid FROM rentals r WHERE r.active = 1 GROUP BY r.client_id ) months_unpaid_sub ON c.client_id = months_unpaid_sub.client_id
                ),
                FilteredData AS (
                    SELECT * FROM ClientData
                    {finalWhereClause}
                ),
                TotalCount AS (
                    SELECT COUNT(*) AS TotalRows FROM FilteredData
                )
                SELECT * FROM FilteredData, TotalCount
                ORDER BY {GetSortColumn(request.SortField).Replace(", PaymentIdentifier", "")} {GetSortDirection(request.SortDirection)}
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            ";

            var dataParameters = new List<SqlParameter>();
            dataParameters.AddRange(filterParameters);
            dataParameters.Add(new SqlParameter("@Offset", (request.PageNumber - 1) * request.PageSize));
            dataParameters.Add(new SqlParameter("@PageSize", request.PageSize));

            DataTable dataTable = await accessDB.GetTableAsync("Clients", fullQuery, dataParameters.ToArray());
            
            int totalCount = dataTable.Rows.Count > 0 ? Convert.ToInt32(dataTable.Rows[0]["TotalRows"]) : 0;

            return (MapDataTableToDto(dataTable), totalCount);
        }

        private string GetSortColumn(string sortField)
        {
            var validSortFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) 
            {
                { "FullName", "FullName" },
                { "Baulera", "Lockers" },
                { "PreviousBalance", "PreviousBalance" },
                { "InterestAmount", "InterestAmount" },
                { "CurrentRent", "CurrentRent" },
                { "Estado", "Status" },
                { "Balance", "Balance" },
                { "PaymentIdentifier", "PaymentIdentifier" },
                { "NextPaymentDay", "NextPaymentDay" }
            };
            
            string primarySort = validSortFields.TryGetValue(sortField, out var dbColumn) ? dbColumn : "Id";
            
            if (primarySort == "PaymentIdentifier") return "PaymentIdentifier";
            
            return $"{primarySort}, PaymentIdentifier";
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
                FullName = row["FullName"]?.ToString() ?? string.Empty,
                Status = row["Status"]?.ToString() ?? "Pendiente",
                PreviousBalance = row["PreviousBalance"] != DBNull.Value ? Convert.ToDecimal(row["PreviousBalance"]) : 0m,
                InterestAmount = row["InterestAmount"] != DBNull.Value ? Convert.ToDecimal(row["InterestAmount"]) : 0m,
                CurrentRent = row["CurrentRent"] != DBNull.Value ? Convert.ToDecimal(row["CurrentRent"]) : 0m,
                Balance = row["Balance"] != DBNull.Value ? Convert.ToDecimal(row["Balance"]) : 0m,
                NextPaymentDay = row["NextPaymentDay"] != DBNull.Value ? Convert.ToDateTime(row["NextPaymentDay"]) : null,
                Lockers = row["Lockers"] != DBNull.Value ? row["Lockers"].ToString()!.Split(',').ToList() : null,
                Active = Convert.ToBoolean(row["Active"]),
                WarehouseLockers = row["WarehouseLockersJson"] != DBNull.Value 
                    ? JsonSerializer.Deserialize<List<WarehouseLockerItem>>(row["WarehouseLockersJson"].ToString()) 
                    : []
            });
        }
        return clients;
    }

        public async Task<List<string>> GetActiveClientNamesAsync()
        {
            var names = new List<string>();
            string query = "SELECT full_name FROM clients WHERE active = 1 ORDER BY full_name";

            DataTable table = await accessDB.GetTableAsync("ClientNames", query);

            foreach (DataRow row in table.Rows)
            {
                names.Add($"{row["full_name"]}");
            }
            return names;
        }

        public async Task<List<string>> SearchActiveClientNamesAsync(string query)
        {
            var names = new List<string>();

            string sqlQuery = @"
                SELECT full_name 
                FROM clients 
                WHERE active = 1 AND 
                    (full_name LIKE @Query)
                ORDER BY full_name
                OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY; -- Limitamos a 10 resultados
            ";

            var parameters = new[] { 
                new SqlParameter("@Query", $"%{query}%")
            };

            DataTable table = await accessDB.GetTableAsync("ClientSearch", sqlQuery, parameters);

            foreach (DataRow row in table.Rows)
            {
                names.Add($"{row["full_name"]}");
            }
            return names;
        }

        public async Task<decimal> GetMaxPaymentIdentifierAsync(SqlConnection connection, SqlTransaction transaction)
        {
             string query = "SELECT ISNULL(MAX(payment_identifier), 0.00) FROM clients";
             using var command = new SqlCommand(query, connection, transaction);
             object result = await command.ExecuteScalarAsync() ?? 0.00m;
             return Convert.ToDecimal(result);
        }
        
        public async Task<Client?> GetClientByIdTransactionAsync(int id, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "SELECT * FROM clients WHERE client_id = @client_id";
            SqlParameter[] parameters = [new SqlParameter("@client_id", SqlDbType.Int) { Value = id }];

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new Client
                        {
                            Id = Convert.ToInt32(reader["client_id"]),
                            PaymentIdentifier = reader["payment_identifier"] != DBNull.Value ? Convert.ToDecimal(reader["payment_identifier"]) : null,
                            FullName = reader["full_name"].ToString() ?? "",
                            RegistrationDate = Convert.ToDateTime(reader["registration_date"]),
                            Notes = reader["notes"] != DBNull.Value ? reader["notes"].ToString() : null,
                            Dni = reader["dni"] != DBNull.Value ? reader["dni"].ToString() : null,
                            Cuit = reader["cuit"] != DBNull.Value ? reader["cuit"].ToString() : null,
                            PreferredPaymentMethodId = reader["preferred_payment_method_id"] != DBNull.Value ? Convert.ToInt32(reader["preferred_payment_method_id"]) : null,
                            IvaCondition = reader["iva_condition"] != DBNull.Value ? reader["iva_condition"].ToString() : null,
                            Active = Convert.ToBoolean(reader["active"]),
                            BillingTypeId = reader["billing_type_id"] != DBNull.Value ? Convert.ToInt32(reader["billing_type_id"]) : null,
                            IncreaseFrequencyMonths = Convert.ToInt32(reader["increase_frequency_months"]),
                            InitialAmount = reader["initial_amount"] != DBNull.Value ? Convert.ToDecimal(reader["initial_amount"]) : null,
                            ReceiveCommunications = reader["receive_communications"] != DBNull.Value && Convert.ToBoolean(reader["receive_communications"])
                        };
                    }
                }
            }
            return null;
        }

        public async Task<bool> ExistsByDniAsync(string dni, int excludeClientId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM clients WHERE dni = @dni AND client_id != @excludeClientId AND active = 1";
            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@dni", SqlDbType.VarChar) { Value = dni });
                command.Parameters.Add(new SqlParameter("@excludeClientId", SqlDbType.Int) { Value = excludeClientId });
                object result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) && Convert.ToInt32(result) > 0;
            }
        }

        // Verifica CUIT existente EXCLUYENDO un ID de cliente
        public async Task<bool> ExistsByCuitAsync(string cuit, int excludeClientId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM clients WHERE cuit = @cuit AND client_id != @excludeClientId AND active = 1";
            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@cuit", SqlDbType.VarChar) { Value = cuit });
                command.Parameters.Add(new SqlParameter("@excludeClientId", SqlDbType.Int) { Value = excludeClientId });
                object result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) > 0 : false;
            }
        }

        public async Task<bool> ExistsByPaymentIdentifierAsync(decimal paymentIdentifier, int excludeClientId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM clients WHERE payment_identifier = @paymentIdentifier AND client_id != @excludeClientId AND active = 1";
            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add(new SqlParameter("@paymentIdentifier", SqlDbType.Decimal) { Value = paymentIdentifier });
                command.Parameters.Add(new SqlParameter("@excludeClientId", SqlDbType.Int) { Value = excludeClientId });
                object result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) > 0 : false;
            }
        }

        public async Task<bool> ExistsByFullNameAsync(string fullName, int excludeClientId, SqlConnection connection, SqlTransaction transaction)
        {
            const string query = "SELECT COUNT(1) FROM clients WHERE full_name = @fullName AND client_id != @excludeClientId AND active = 1";
            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add(new SqlParameter("@fullName", SqlDbType.VarChar) { Value = fullName });
            command.Parameters.Add(new SqlParameter("@excludeClientId", SqlDbType.Int) { Value = excludeClientId });
            int count = (int)await command.ExecuteScalarAsync();
            return count > 0;
        }


        // Actualiza un cliente DENTRO de una transacción
        public async Task<bool> UpdateClientTransactionAsync(Client client, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                UPDATE clients SET
                    payment_identifier = @payment_identifier,
                    full_name = @full_name,
                    registration_date = @registration_date,
                    dni = @dni,
                    cuit = @cuit,
                    preferred_payment_method_id = @preferred_payment_method_id,
                    iva_condition = @iva_condition,
                    notes = @notes,
                    billing_type_id = @billing_type_id,
                    increase_frequency_months = @increase_frequency_months,
                    initial_amount = @initial_amount,
                    receive_communications = @ReceiveCommunications
                    
                    -- Si tu tabla tiene la columna del próximo aumento, descomenta esta línea:
                    -- , next_increase_day = @next_increase_day
                WHERE client_id = @client_id";

            SqlParameter[] parameters =
            [
                new("@payment_identifier", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = (object?)client.PaymentIdentifier ?? DBNull.Value },
                new("@full_name", SqlDbType.VarChar) { Value = (object?)client.FullName?.Trim() ?? DBNull.Value },
                new("@registration_date", SqlDbType.DateTime) { Value = client.RegistrationDate },
                new("@dni", SqlDbType.VarChar) { Value = (object?)client.Dni?.Trim() ?? DBNull.Value },
                new("@cuit", SqlDbType.VarChar) { Value = (object?)client.Cuit?.Trim() ?? DBNull.Value },
                new("@preferred_payment_method_id", SqlDbType.Int) { Value = (object?)client.PreferredPaymentMethodId ?? DBNull.Value },
                new("@iva_condition", SqlDbType.VarChar) { Value = (object?)client.IvaCondition?.Trim() ?? DBNull.Value },
                new("@notes", SqlDbType.VarChar) { Value = (object?)client.Notes?.Trim() ?? DBNull.Value },
                new("@billing_type_id", SqlDbType.Int) { Value = (object?)client.BillingTypeId ?? DBNull.Value },
                new("@increase_frequency_months", SqlDbType.Int) { Value = client.IncreaseFrequencyMonths },
                new("@initial_amount", SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = (object?)client.InitialAmount ?? DBNull.Value },
                new("@client_id", SqlDbType.Int) { Value = client.Id },
                new("@ReceiveCommunications", SqlDbType.Bit) { Value = client.ReceiveCommunications },
                // new("@next_increase_day", SqlDbType.DateTime) { Value = (object?)client.NextIncreaseDay ?? DBNull.Value }
            ];

            using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.AddRange(parameters);
            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        public async Task<bool> ReactivateClientTransactionAsync(int clientId, decimal newPaymentIdentifier, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                UPDATE clients 
                SET active = 1, 
                    payment_identifier = @newPaymentIdentifier 
                WHERE client_id = @clientId";

            SqlParameter[] parameters =
            [
                new ("@clientId", SqlDbType.Int) { Value = clientId },
                new ("@newPaymentIdentifier", SqlDbType.Decimal) { Value = newPaymentIdentifier }
            ];

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                int rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        public async Task<List<ClientLockerHistory>> GetClientLockerHistoryAsync(int clientId)
        {
            var list = new List<ClientLockerHistory>();
            
            string query = @"
                SELECT 
                    h.history_id, 
                    l.identifier AS locker_identifier, 
                    w.name AS warehouse_name, 
                    lt.name AS locker_type, 
                    h.start_date, 
                    h.end_date, 
                    h.notes
                FROM client_locker_history h
                INNER JOIN lockers l ON h.locker_id = l.locker_id
                INNER JOIN warehouses w ON l.warehouse_id = w.warehouse_id
                LEFT JOIN locker_types lt ON l.locker_type_id = lt.locker_type_id
                WHERE h.client_id = @ClientId
                ORDER BY h.start_date DESC"; 

            var dt = await accessDB.GetTableAsync("LockerHistory", query, new[] {
                new SqlParameter("@ClientId", clientId)
            });

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new ClientLockerHistory
                {
                    Id = Convert.ToInt32(row["history_id"]),
                    LockerIdentifier = row["locker_identifier"].ToString(),
                    WarehouseName = row["warehouse_name"].ToString(),
                    LockerType = row["locker_type"] != DBNull.Value ? row["locker_type"].ToString() : "N/A",
                    StartDate = Convert.ToDateTime(row["start_date"]),
                    EndDate = row["end_date"] != DBNull.Value ? Convert.ToDateTime(row["end_date"]) : null,
                    Notes = row["notes"] != DBNull.Value ? row["notes"].ToString() : null
                });
            }
            return list;
        }

        public async Task CloseLockerHistoryTransactionAsync(int clientId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerIds == null || lockerIds.Count == 0) return;

            string ids = string.Join(",", lockerIds);
            string query = $@"
                UPDATE client_locker_history 
                SET end_date = GETDATE() 
                WHERE client_id = @ClientId AND locker_id IN ({ids}) AND end_date IS NULL";

            using (var cmd = new SqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@ClientId", clientId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task OpenLockerHistoryTransactionAsync(int clientId, List<int> lockerIds, SqlConnection connection, SqlTransaction transaction)
        {
            if (lockerIds == null || lockerIds.Count == 0) return;

            foreach (var lockerId in lockerIds)
            {
                string query = @"
                    INSERT INTO client_locker_history (client_id, locker_id, start_date) 
                    VALUES (@ClientId, @LockerId, GETDATE())";

                using (var cmd = new SqlCommand(query, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                    cmd.Parameters.AddWithValue("@LockerId", lockerId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}

