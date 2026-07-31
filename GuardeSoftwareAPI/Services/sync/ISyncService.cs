using GuardeSoftwareAPI.Dtos.Sync;

namespace GuardeSoftwareAPI.Services.sync
{
    public interface ISyncService
    {
        Task<SyncSnapshotDto> GetSnapshotAsync();
        Task<SyncPaymentsResponseDto> ProcessOfflinePaymentsAsync(SyncPaymentsRequestDto request);
    }
}
