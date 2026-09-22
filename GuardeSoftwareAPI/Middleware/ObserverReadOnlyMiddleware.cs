using System.Globalization;
using GuardeSoftwareAPI.Dao;

namespace GuardeSoftwareAPI.Middleware;

public sealed class ObserverReadOnlyMiddleware
{
    private readonly RequestDelegate _next;

    public ObserverReadOnlyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, DaoUser daoUser)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !context.Request.Path.StartsWithSegments("/api")
            || !TryGetBusinessUserId(context, out int businessUserId)
            || !await daoUser.IsObserverAsync(businessUserId))
        {
            await _next(context);
            return;
        }

        if (IsAllowedObserverMutation(context.Request))
        {
            await _next(context);
            return;
        }

        bool isCashRequest = context.Request.Path.StartsWithSegments("/api/cashflow");
        bool isConfigurationRequest = IsConfigurationRequest(context.Request.Path);
        bool isStatisticsRequest = context.Request.Path.StartsWithSegments("/api/statistics/monthly");
        bool isReadOnlyMethod = HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method);

        if (!isCashRequest && !isConfigurationRequest && !isStatisticsRequest && isReadOnlyMethod)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            message = isCashRequest
                ? "El usuario Observador no tiene acceso a Caja."
                : isConfigurationRequest
                    ? "El usuario Observador no tiene acceso a Configuración."
                    : isStatisticsRequest
                        ? "El usuario Observador no tiene acceso a Estadísticas."
                        : "El usuario Observador tiene acceso de solo lectura."
        });
    }

    private static bool IsAllowedObserverMutation(HttpRequest request)
    {
        if (!HttpMethods.IsPut(request.Method))
        {
            return false;
        }

        string[] segments = request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            ?? [];

        return segments.Length == 4
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("client", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out int clientId)
            && clientId > 0
            && segments[3].Equals("comment", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfigurationRequest(PathString path)
    {
        return path.StartsWithSegments("/api/user")
            || path.StartsWithSegments("/api/usertype")
            || path.StartsWithSegments("/api/smtpconfigurations")
            || path.StartsWithSegments("/api/activitylog")
            || path.StartsWithSegments("/api/masscommunicationrecipients");
    }

    private static bool TryGetBusinessUserId(HttpContext context, out int userId)
    {
        string? value = context.User.FindFirst("businessUserId")?.Value;
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out userId);
    }
}
