using System.Collections.Concurrent;
using System.Data;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Client;

namespace GuardeSoftwareAPI.Services.client;

public partial class ClientService
{
    // Each client runs the full statement calculation (a few queries), so keep the
    // load on the database bounded.
    private const int ProjectedNextMonthParallelism = 4;

    public async Task<ClientProjectedNextMonthResultDto> GetProjectedNextMonthAsync(CancellationToken cancellationToken = default)
    {
        DateTime argentinaNow = DateTime.UtcNow.AddHours(-3);
        var result = new ClientProjectedNextMonthResultDto
        {
            TargetMonth = new DateTime(argentinaNow.Year, argentinaNow.Month, 1).AddMonths(1),
            GeneratedAt = DateTime.UtcNow
        };

        DataTable table = await accessDB.GetTableAsync("ProjectedClients", @"
            SELECT DISTINCT c.client_id
            FROM clients c
            JOIN rentals r ON r.client_id = c.client_id AND r.active = 1
            WHERE c.active = 1 AND c.is_deleted = 0");
        var clientIds = table.Rows.Cast<DataRow>().Select(row => Convert.ToInt32(row["client_id"])).ToList();

        // The same method that builds the projected statement e-mail: what Clients
        // shows is what the client would receive.
        var statements = new CommunicationDao(accessDB);
        var items = new ConcurrentBag<ClientProjectedNextMonthDto>();
        var failed = new ConcurrentBag<int>();

        await Parallel.ForEachAsync(
            clientIds,
            new ParallelOptions { MaxDegreeOfParallelism = ProjectedNextMonthParallelism, CancellationToken = cancellationToken },
            async (clientId, _) =>
            {
                try
                {
                    var statement = await statements.GetClientFinancialData(clientId, isNextMonth: true);
                    items.Add(new ClientProjectedNextMonthDto
                    {
                        ClientId = clientId,
                        PreviousBalance = Negate(statement.PreviousBalance),
                        InterestAmount = statement.Surcharge,
                        CurrentRent = statement.Rent,
                        Balance = Negate(statement.NetDebt),
                        StatementTotal = statement.CurrentBalance,
                        IncreasePercentage = statement.RentIncreasePercentage,
                        IncreasePercentageMissing = statement.RentIncreasePercentageMissing
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not project the next month for client {ClientId}", clientId);
                    failed.Add(clientId);
                }
            });

        result.Items = items.OrderBy(item => item.ClientId).ToList();
        result.FailedClientIds = failed.OrderBy(id => id).ToList();
        return result;
    }

    private static decimal Negate(decimal amount) => amount == 0m ? 0m : -amount;
}
