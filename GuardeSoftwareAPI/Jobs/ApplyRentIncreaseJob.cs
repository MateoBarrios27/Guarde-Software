//This jobs is not currently in use, but kept for future reference.
// using GuardeSoftwareAPI.Dao;
// using Quartz;
// using System.Data;
// using System.Threading.Tasks;

// //This atribute prevents the job from being executed concurrently
// [DisallowConcurrentExecution]
// public class ApplyRentIncreaseJob : IJob
// {
//     private readonly ILogger<ApplyRentIncreaseJob> _logger;
//     private readonly DaoRental _daoRental;

//     public ApplyRentIncreaseJob(ILogger<ApplyRentIncreaseJob> logger, AccessDB accessDB)
//     {
//         _logger = logger;
//         _daoRental = new DaoRental(accessDB);
//     }

//     public async Task Execute(IJobExecutionContext context)
//     {
//         _logger.LogInformation("Initializating rent increase job. Hour: {time}", DateTimeOffset.Now);

//         try
//         {
//             // 1. get rentals that need a rent increase today
//             DataTable rentalsToUpdate = await _daoRental.GetRentalsDueForIncreaseAsync();

//             if (rentalsToUpdate.Rows.Count == 0)
//             {
//                 _logger.LogInformation("No rents to increase amount today.");
//                 return;
//             }

//             _logger.LogInformation("{count} rents to increase amount founded.", rentalsToUpdate.Rows.Count);

//             // 2. Apply the increase to each rental
//             foreach (DataRow row in rentalsToUpdate.Rows)
//             {
//                 int rentalId = Convert.ToInt32(row["rental_id"]);
//                 decimal oldAmount = Convert.ToDecimal(row["current_amount"]);
//                 decimal percentage = Convert.ToDecimal(row["percentage"]);
//                 int oldHistoryId = Convert.ToInt32(row["rental_amount_history_id"]);

//                 // 1. We calculate the new amount
//                 decimal calculatedAmount = oldAmount * (1 + percentage / 100.0m);
//                 // 2. We round it to the nearest 10
//                 decimal newAmount = Math.Ceiling(calculatedAmount / 10.0m) * 10.0m;

//                 await _daoRental.ApplyRentIncreaseAsync(rentalId, newAmount, oldHistoryId);

//                 _logger.LogInformation("Increase applied to rental ID: {rentalId}. Original amount: {calculatedAmount}, rounded amount: {newAmount}", rentalId, calculatedAmount, newAmount);
//             }

//              _logger.LogInformation("--- Apply rent increase job complete ---");
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "An error has ocurred during debits apply job.");
//             //Quartz can retry the job or mark it as failed based on your configuration
//         }
//     }
// }