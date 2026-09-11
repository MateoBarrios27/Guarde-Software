using GuardeSoftwareAPI.Dtos.Communication;

namespace GuardeSoftwareAPI.Services.communication
{
    public interface IReceiptDeliveryService
    {
        Task<ReceiptDeliveryResult> SendAsync(
            ReceiptDeliveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
