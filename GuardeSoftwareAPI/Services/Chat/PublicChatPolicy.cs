using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;

namespace GuardeSoftwareAPI.Services.chat
{
    /// <summary>
    /// Protección de la API pública del chat, que puede llamar cualquiera desde Internet:
    /// CORS acotado a los dominios de la web y límites de uso por IP. Los límites por
    /// conversación (mensajes por minuto, emails por día) viven en <see cref="ChatService"/>.
    /// </summary>
    public static class PublicChatPolicy
    {
        public const string Name = "PublicChat";
        public const string StartLimiter = "chat-start";
        public const string ReadLimiter = "chat-read";
        public const string WriteLimiter = "chat-write";

        /// <summary>Se puede reemplazar con la sección de configuración Chat:AllowedOrigins.</summary>
        public static readonly string[] DefaultAllowedOrigins =
        [
            "https://www.guardeloquequiera.net",
            "https://guardeloquequiera.net",
            "https://*.wixsite.com"
        ];

        public static string[] ResolveAllowedOrigins(IConfiguration configuration)
        {
            string[]? configured = configuration.GetSection("Chat:AllowedOrigins").Get<string[]>();
            return configured is { Length: > 0 } ? configured : DefaultAllowedOrigins;
        }

        public static void ConfigureCors(CorsPolicyBuilder policy, IConfiguration configuration)
        {
            // Sin credenciales: el visitante se identifica con su token, no con cookies.
            policy.WithOrigins(ResolveAllowedOrigins(configuration))
                  .SetIsOriginAllowedToAllowWildcardSubdomains()
                  .AllowAnyHeader()
                  .WithMethods("GET", "POST");
        }

        public static void ConfigureRateLimiter(RateLimiterOptions options)
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(StartLimiter, context => PerIp(context, permitLimit: 20, TimeSpan.FromMinutes(10)));
            options.AddPolicy(WriteLimiter, context => PerIp(context, permitLimit: 60, TimeSpan.FromMinutes(1)));
            options.AddPolicy(ReadLimiter, context => PerIp(context, permitLimit: 120, TimeSpan.FromMinutes(1)));
        }

        private static RateLimitPartition<string> PerIp(HttpContext context, int permitLimit, TimeSpan window)
        {
            string key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            });
        }
    }
}
