using GuardeSoftwareAPI.Dtos.Communication;

namespace GuardeSoftwareAPI.Services.communication
{
    /// <summary>
    /// Temporary product policy: every new or edited communication is sent by
    /// Email only. Historical WhatsApp data is preserved, but it cannot be used
    /// to create, schedule or execute new deliveries while this policy is active.
    /// </summary>
    public static class CommunicationChannelPolicy
    {
        public const string Email = "Email";
        public const string WhatsApp = "WhatsApp";
        public const bool WhatsAppSendingEnabled = false;

        public static void ApplyTo(UpsertCommunicationRequest request)
        {
            request.Channels = [Email];
        }

        public static bool HasEmailChannel(string? channelSummary)
        {
            return !string.IsNullOrWhiteSpace(channelSummary)
                && channelSummary.Contains(Email, StringComparison.OrdinalIgnoreCase);
        }
    }
}
