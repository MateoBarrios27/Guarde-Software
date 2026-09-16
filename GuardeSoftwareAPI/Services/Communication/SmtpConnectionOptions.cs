using GuardeSoftwareAPI.Dtos.Communication;
using MailKit.Security;

namespace GuardeSoftwareAPI.Services.communication;

internal static class SmtpConnectionOptions
{
    public static SecureSocketOptions Resolve(SmtpSettingsModel settings)
    {
        if (!settings.UseSsl)
            return SecureSocketOptions.None;

        return settings.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.StartTlsWhenAvailable
        };
    }
}
