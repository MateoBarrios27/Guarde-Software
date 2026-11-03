using GuardeSoftwareAPI.Dtos.Chat;

namespace GuardeSoftwareAPI.Services.chat
{
    /// <param name="DayOfWeek">1 = lunes ... 7 = domingo.</param>
    public sealed record ChatBusinessDay(int DayOfWeek, bool IsOpen, TimeSpan? OpensAt, TimeSpan? ClosesAt);

    public sealed record ChatAvailability(bool IsOpen, DateTime? ClosesAtLocal, DateTime? NextOpenAtLocal, DateTime LocalNow);

    /// <summary>
    /// Horario laboral del chat. Es lógica pura: recibe el instante en UTC y lo
    /// evalúa con la hora de Argentina (UTC-3 fijo, igual que el resto del backend).
    /// El horario es semiabierto: abre a la hora de apertura y cierra, sin atender,
    /// a la hora de cierre.
    /// </summary>
    public sealed class ChatSchedule
    {
        public static readonly TimeSpan ArgentinaOffset = TimeSpan.FromHours(-3);

        private static readonly string[] DayNames =
            ["lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo"];
        private static readonly string[] ShortDayNames =
            ["Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom"];

        private readonly ChatBusinessDay[] _days;

        public ChatSchedule(IEnumerable<ChatBusinessDay> days)
        {
            // Los días que falten se consideran cerrados.
            _days = Enumerable.Range(1, 7).Select(day => new ChatBusinessDay(day, false, null, null)).ToArray();

            foreach (ChatBusinessDay day in days)
            {
                if (day.DayOfWeek is < 1 or > 7)
                    throw new ChatValidationException("El día de la semana debe estar entre 1 (lunes) y 7 (domingo).");
                if (day.IsOpen)
                    ValidateWindow(day);

                _days[day.DayOfWeek - 1] = day.IsOpen ? day : new ChatBusinessDay(day.DayOfWeek, false, null, null);
            }
        }

        public IReadOnlyList<ChatBusinessDay> Days => _days;

        public static DateTime ToLocal(DateTime utc)
        {
            DateTime asUtc = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc;
            return DateTime.SpecifyKind(asUtc + ArgentinaOffset, DateTimeKind.Unspecified);
        }

        public static int IsoDayOfWeek(DateTime date)
            => date.DayOfWeek == System.DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        public ChatAvailability Evaluate(DateTime utcNow)
        {
            DateTime local = ToLocal(utcNow);
            ChatBusinessDay today = _days[IsoDayOfWeek(local) - 1];

            if (today.IsOpen
                && local.TimeOfDay >= today.OpensAt!.Value
                && local.TimeOfDay < today.ClosesAt!.Value)
            {
                return new ChatAvailability(true, local.Date + today.ClosesAt.Value, null, local);
            }

            // Se busca la próxima apertura estrictamente posterior a este instante.
            for (int offset = 0; offset <= 7; offset++)
            {
                DateTime date = local.Date.AddDays(offset);
                ChatBusinessDay day = _days[IsoDayOfWeek(date) - 1];
                if (!day.IsOpen) continue;

                DateTime opening = date + day.OpensAt!.Value;
                if (opening > local)
                    return new ChatAvailability(false, null, opening, local);
            }

            return new ChatAvailability(false, null, null, local);
        }

        public ChatStatusDto ToStatus(DateTime utcNow, int responseTimeMinutes)
        {
            ChatAvailability availability = Evaluate(utcNow);
            return new ChatStatusDto
            {
                IsOpen = availability.IsOpen,
                ResponseTimeMinutes = responseTimeMinutes,
                ClosesAtLabel = availability.ClosesAtLocal is { } closes ? $"{FormatTime(closes.TimeOfDay)} hs" : null,
                NextOpenLabel = availability.NextOpenAtLocal is { } next
                    ? DescribeNextOpening(availability.LocalNow, next)
                    : null,
                ScheduleLabel = Describe()
            };
        }

        public static string FormatTime(TimeSpan time) => $"{(int)time.TotalHours}:{time.Minutes:00}";

        private static string DescribeNextOpening(DateTime localNow, DateTime next)
        {
            int days = (next.Date - localNow.Date).Days;
            string time = $"{FormatTime(next.TimeOfDay)} hs";
            return days switch
            {
                0 => $"hoy a las {time}",
                1 => $"mañana a las {time}",
                _ => $"el {DayNames[IsoDayOfWeek(next) - 1]} a las {time}"
            };
        }

        /// <summary>Agrupa días consecutivos con la misma ventana: "Lun a vie 9:30 a 15:30 hs · Sáb 9:30 a 12:30 hs".</summary>
        public string Describe()
        {
            var parts = new List<string>();
            int index = 0;
            while (index < _days.Length)
            {
                ChatBusinessDay first = _days[index];
                if (!first.IsOpen) { index++; continue; }

                int last = index;
                while (last + 1 < _days.Length
                       && _days[last + 1].IsOpen
                       && _days[last + 1].OpensAt == first.OpensAt
                       && _days[last + 1].ClosesAt == first.ClosesAt)
                {
                    last++;
                }

                string range = last == index
                    ? ShortDayNames[index]
                    : $"{ShortDayNames[index]} a {ShortDayNames[last].ToLowerInvariant()}";
                parts.Add($"{range} {FormatTime(first.OpensAt!.Value)} a {FormatTime(first.ClosesAt!.Value)} hs");
                index = last + 1;
            }

            return parts.Count == 0 ? "Sin horario de atención configurado" : string.Join(" · ", parts);
        }

        public static void ValidateWindow(ChatBusinessDay day)
        {
            if (day.OpensAt is null || day.ClosesAt is null)
                throw new ChatValidationException("Un día abierto necesita hora de apertura y de cierre.");
            // Un horario no puede pasar de medianoche: SQL TIME llega hasta 23:59.
            if (day.OpensAt.Value < TimeSpan.Zero || day.ClosesAt.Value >= TimeSpan.FromHours(24)
                || day.OpensAt.Value >= day.ClosesAt.Value)
                throw new ChatValidationException("La hora de apertura debe ser anterior a la de cierre.");
        }
    }
}
