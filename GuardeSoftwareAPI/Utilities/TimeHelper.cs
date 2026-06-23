using System;

namespace GuardeSoftwareAPI.Utils
{
    public static class TimeHelper
    {
        public static DateTime GetArgentinaTime()
        {
            try
            {
                // Formato IANA (Servidores Linux / Azure)
                var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch (TimeZoneNotFoundException)
            {
                // Formato Windows (Localhost)
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
        }
    }
}   