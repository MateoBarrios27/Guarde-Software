using System.Globalization;
using GuardeSoftwareAPI.Dao;
using GuardeSoftwareAPI.Dtos.Chat;

namespace GuardeSoftwareAPI.Services.chat
{
    public sealed class ChatService : IChatService
    {
        public const int ResponseTimeMinutes = 2;
        public const int MaxNameLength = 100;
        public const int MaxVisitorMessageLength = 1000;
        public const int MaxStaffMessageLength = 2000;
        public const int MaxMessagesPerMinute = 8;
        public const int MaxMessagesPerDay = 150;
        public const int MaxConversationsPerContactPerHour = 3;
        public const int MaxEmailsPerConversationPerDay = 5;

        private const int MaxMessagesLoaded = 500;
        private static readonly TimeSpan EmailTimeout = TimeSpan.FromSeconds(20);

        private readonly DaoChat _dao;
        private readonly IChatRealtime _realtime;
        private readonly IChatPresence _presence;
        private readonly IChatEmailNotifier _emailNotifier;
        private readonly TimeProvider _time;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            DaoChat dao,
            IChatRealtime realtime,
            IChatPresence presence,
            IChatEmailNotifier emailNotifier,
            TimeProvider time,
            ILogger<ChatService> logger)
        {
            _dao = dao;
            _realtime = realtime;
            _presence = presence;
            _emailNotifier = emailNotifier;
            _time = time;
            _logger = logger;
        }

        private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        // ================================================================ visitante

        public async Task<ChatStatusDto> GetStatusAsync()
        {
            ChatSchedule schedule = await LoadScheduleAsync();
            return schedule.ToStatus(UtcNow, ResponseTimeMinutes);
        }

        public async Task<ChatVisitorConversationDto> StartConversationAsync(StartChatRequest request)
        {
            // Una persona nunca completa el campo señuelo; un bot de formularios sí.
            if (!string.IsNullOrWhiteSpace(request.Website))
                throw new ChatValidationException("No se pudo enviar el mensaje.");

            string name = ChatText.Clean(request.Name, allowNewLines: false);
            if (name.Length < 2)
                throw new ChatValidationException("Contanos tu nombre para poder responderte.");
            if (name.Length > MaxNameLength)
                throw new ChatValidationException($"El nombre admite hasta {MaxNameLength} caracteres.");

            string? email = ChatText.NormalizeEmail(request.Email);
            string? phone = ChatText.NormalizePhone(request.Phone);
            if (email is null && phone is null)
                throw new ChatValidationException("Dejanos un email o un teléfono para poder responderte.");

            string message = RequireMessage(request.Message, MaxVisitorMessageLength);
            DateTime now = UtcNow;

            int recent = await _dao.CountRecentConversationsAsync(email, phone, now.AddHours(-1));
            if (recent >= MaxConversationsPerContactPerHour)
                throw new ChatRateLimitException("Ya iniciaste varias conversaciones. Esperá un rato antes de abrir otra.");

            ChatSchedule schedule = await LoadScheduleAsync();
            bool outOfHours = !schedule.Evaluate(now).IsOpen;

            string token = ChatVisitorToken.Create();
            var created = await _dao.CreateConversationAsync(
                ChatVisitorToken.Hash(token),
                name,
                email,
                phone,
                ChatText.NormalizeSourceUrl(request.SourceUrl),
                message,
                outOfHours,
                now);

            ChatMessageDto first = (await _dao.GetMessageAsync(created.MessageId))!;
            await PublishMessageAsync(created.ConversationId, first);

            return new ChatVisitorConversationDto
            {
                ConversationId = created.PublicId,
                VisitorToken = token,
                Status = "open",
                VisitorName = name,
                UnreadCount = 0,
                LastActivityAt = first.CreatedAt,
                Messages = [ToVisitorView(first)],
                Chat = schedule.ToStatus(now, ResponseTimeMinutes)
            };
        }

        public async Task<ChatVisitorConversationDto> GetVisitorConversationAsync(
            Guid publicId, string? token, long afterMessageId)
        {
            ChatConversationRecord record = await RequireVisitorAsync(publicId, token);
            DateTime now = UtcNow;

            await _dao.TouchVisitorAsync(record.Id, now);
            List<ChatMessageDto> messages = await _dao.GetMessagesAsync(record.Id, afterMessageId, MaxMessagesLoaded);
            ChatSchedule schedule = await LoadScheduleAsync();

            return new ChatVisitorConversationDto
            {
                ConversationId = record.PublicId,
                Status = record.Status,
                VisitorName = record.VisitorName,
                UnreadCount = await _dao.CountVisitorUnreadAsync(record.Id),
                LastActivityAt = messages.Count > 0 ? messages[^1].CreatedAt : now,
                Messages = messages.Select(ToVisitorView).ToList(),
                Chat = schedule.ToStatus(now, ResponseTimeMinutes)
            };
        }

        public async Task<ChatVisitorMessageDto> SendVisitorMessageAsync(Guid publicId, string? token, string? body)
        {
            ChatConversationRecord record = await RequireVisitorAsync(publicId, token);
            string message = RequireMessage(body, MaxVisitorMessageLength);
            DateTime now = UtcNow;

            if (await _dao.CountVisitorMessagesSinceAsync(record.Id, now.AddMinutes(-1)) >= MaxMessagesPerMinute)
                throw new ChatRateLimitException("Estás escribiendo muy rápido. Esperá unos segundos.");
            if (await _dao.CountVisitorMessagesSinceAsync(record.Id, now.AddDays(-1)) >= MaxMessagesPerDay)
                throw new ChatRateLimitException("Llegaste al máximo de mensajes de hoy. Volvé a escribirnos mañana.");

            ChatSchedule schedule = await LoadScheduleAsync();
            bool outOfHours = !schedule.Evaluate(now).IsOpen;

            long messageId = await _dao.AddVisitorMessageAsync(record.Id, message, outOfHours, now);
            ChatMessageDto saved = (await _dao.GetMessageAsync(messageId))!;
            await PublishMessageAsync(record.Id, saved);

            return ToVisitorView(saved);
        }

        public async Task MarkVisitorReadAsync(Guid publicId, string? token, long? upToMessageId)
        {
            ChatConversationRecord record = await RequireVisitorAsync(publicId, token);
            await _dao.MarkVisitorReadAsync(record.Id, upToMessageId, UtcNow);
        }

        public async Task AuthenticateVisitorAsync(Guid publicId, string? token)
            => await RequireVisitorAsync(publicId, token);

        // ============================================================ administración

        public async Task<List<ChatConversationSummaryDto>> GetConversationsAsync(string? status, string? search)
        {
            string? normalizedStatus = status?.Trim().ToLowerInvariant() switch
            {
                null or "" or "all" => null,
                "open" => "open",
                "closed" => "closed",
                _ => throw new ChatValidationException("El estado debe ser open, closed o all.")
            };

            // La bandeja de cerrados es historial: alcanza con los más recientes.
            int take = normalizedStatus == "closed" ? 100 : 300;
            List<ChatConversationSummaryDto> items = await _dao.ListAsync(normalizedStatus, search, take);
            foreach (ChatConversationSummaryDto item in items)
                item.VisitorOnline = _presence.IsVisitorOnline(item.PublicId);

            return items;
        }

        public async Task<ChatConversationDetailDto> GetConversationAsync(long conversationId)
        {
            ChatConversationSummaryDto summary = await RequireSummaryAsync(conversationId);
            return new ChatConversationDetailDto
            {
                Conversation = summary,
                Messages = await _dao.GetMessagesAsync(conversationId, 0, MaxMessagesLoaded)
            };
        }

        public async Task<ChatInboxSummaryDto> GetInboxSummaryAsync()
        {
            ChatInboxCounts counts = await _dao.GetInboxCountsAsync();
            ChatSchedule schedule = await LoadScheduleAsync();

            return new ChatInboxSummaryDto
            {
                AwaitingReplyCount = counts.AwaitingReplyCount,
                UnreadConversationCount = counts.UnreadConversationCount,
                OldestAwaitingSince = counts.OldestAwaitingSince,
                IsOpenNow = schedule.Evaluate(UtcNow).IsOpen,
                ResponseTimeMinutes = ResponseTimeMinutes
            };
        }

        public async Task MarkReadAsync(long conversationId, long? upToMessageId)
        {
            await RequireRecordAsync(conversationId);
            await _dao.MarkStaffReadAsync(conversationId, upToMessageId);
            await SafeAsync(async () =>
                await _realtime.ConversationChangedAsync(await RequireSummaryAsync(conversationId)));
        }

        public async Task<ChatStaffReplyResultDto> ReplyAsync(
            long conversationId, int userId, string? body, CancellationToken cancellationToken = default)
        {
            string text = RequireMessage(body, MaxStaffMessageLength);
            ChatConversationRecord record = await RequireRecordAsync(conversationId);
            if (record.Status != "open")
                throw new ChatConflictException("La conversación está cerrada. Reabrila para responder.");

            DateTime now = UtcNow;
            long messageId = await _dao.AddStaffMessageAsync(conversationId, userId, text, now);

            // El visitante ve la respuesta enseguida; el email llega después y no la demora.
            ChatMessageDto saved = (await _dao.GetMessageAsync(messageId))!;
            await PublishMessageAsync(conversationId, saved);

            string emailStatus = await NotifyByEmailAsync(record, text, now, cancellationToken);
            await _dao.SetEmailStatusAsync(messageId, emailStatus, UtcNow);
            saved.EmailStatus = emailStatus;

            return new ChatStaffReplyResultDto
            {
                Message = saved,
                Conversation = await RequireSummaryAsync(conversationId)
            };
        }

        public async Task<ChatConversationSummaryDto> CloseAsync(long conversationId, int userId)
        {
            await RequireRecordAsync(conversationId);
            await _dao.CloseAsync(conversationId, userId, UtcNow);
            return await PublishStatusAsync(conversationId);
        }

        public async Task<ChatConversationSummaryDto> ReopenAsync(long conversationId)
        {
            await RequireRecordAsync(conversationId);
            await _dao.ReopenAsync(conversationId);
            return await PublishStatusAsync(conversationId);
        }

        public async Task<ChatBusinessHoursDto> GetBusinessHoursAsync()
        {
            ChatSchedule schedule = await LoadScheduleAsync();
            return ToBusinessHoursDto(schedule);
        }

        public async Task<ChatBusinessHoursDto> UpdateBusinessHoursAsync(UpdateChatBusinessHoursRequest request, int userId)
        {
            if (request.Days is null || request.Days.Count == 0)
                throw new ChatValidationException("Falta el horario de atención.");
            if (request.Days.Select(day => day.DayOfWeek).Distinct().Count() != request.Days.Count)
                throw new ChatValidationException("Hay días repetidos en el horario.");

            var days = new List<ChatBusinessDay>(request.Days.Count);
            foreach (ChatBusinessDayDto dto in request.Days)
            {
                if (!dto.IsOpen)
                {
                    days.Add(new ChatBusinessDay(dto.DayOfWeek, false, null, null));
                    continue;
                }

                days.Add(new ChatBusinessDay(dto.DayOfWeek, true, ParseTime(dto.OpensAt), ParseTime(dto.ClosesAt)));
            }

            // Construir el horario valida cada día antes de tocar la base.
            var schedule = new ChatSchedule(days);
            await _dao.SaveScheduleAsync(schedule.Days, userId, UtcNow);
            return ToBusinessHoursDto(schedule);
        }

        // ================================================================ internos

        private async Task<ChatSchedule> LoadScheduleAsync()
            => new(await _dao.GetScheduleAsync());

        private ChatBusinessHoursDto ToBusinessHoursDto(ChatSchedule schedule) => new()
        {
            Days = schedule.Days.Select(day => new ChatBusinessDayDto
            {
                DayOfWeek = day.DayOfWeek,
                IsOpen = day.IsOpen,
                OpensAt = day.OpensAt is { } opens ? FormatTime(opens) : null,
                ClosesAt = day.ClosesAt is { } closes ? FormatTime(closes) : null
            }).ToList(),
            Status = schedule.ToStatus(UtcNow, ResponseTimeMinutes)
        };

        private static string FormatTime(TimeSpan time) => $"{(int)time.TotalHours:00}:{time.Minutes:00}";

        private static TimeSpan ParseTime(string? value)
        {
            if (!TimeSpan.TryParseExact(value?.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out TimeSpan time))
                throw new ChatValidationException("Las horas deben tener el formato HH:mm.");

            return time;
        }

        private static string RequireMessage(string? raw, int maxLength)
        {
            string message = ChatText.Clean(raw, allowNewLines: true);
            if (message.Length == 0)
                throw new ChatValidationException("Escribí un mensaje.");
            if (message.Length > maxLength)
                throw new ChatValidationException($"El mensaje admite hasta {maxLength} caracteres.");

            return message;
        }

        private async Task<ChatConversationRecord> RequireVisitorAsync(Guid publicId, string? token)
        {
            ChatConversationRecord? record = await _dao.FindByPublicIdAsync(publicId);
            if (record is null || !ChatVisitorToken.Matches(record.TokenHash, token))
                throw new ChatNotFoundException();

            return record;
        }

        private async Task<ChatConversationRecord> RequireRecordAsync(long conversationId)
            => await _dao.FindByIdAsync(conversationId) ?? throw new ChatNotFoundException();

        private async Task<ChatConversationSummaryDto> RequireSummaryAsync(long conversationId)
        {
            ChatConversationSummaryDto summary = await _dao.GetSummaryAsync(conversationId)
                ?? throw new ChatNotFoundException();
            summary.VisitorOnline = _presence.IsVisitorOnline(summary.PublicId);
            return summary;
        }

        private static ChatVisitorMessageDto ToVisitorView(ChatMessageDto message) => new()
        {
            Id = message.Id,
            SenderType = message.SenderType,
            Body = message.Body,
            CreatedAt = message.CreatedAt
        };

        private async Task PublishMessageAsync(long conversationId, ChatMessageDto message)
        {
            await SafeAsync(async () =>
            {
                ChatConversationSummaryDto summary = await RequireSummaryAsync(conversationId);
                await _realtime.MessageAddedAsync(summary, message, ToVisitorView(message));
            });
        }

        private async Task<ChatConversationSummaryDto> PublishStatusAsync(long conversationId)
        {
            ChatConversationSummaryDto summary = await RequireSummaryAsync(conversationId);
            await SafeAsync(() => _realtime.StatusChangedAsync(summary));
            return summary;
        }

        /// <summary>La difusión es una comodidad: si falla, el mensaje ya está guardado y se ve al recargar.</summary>
        private async Task SafeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo difundir un evento del chat en tiempo real.");
            }
        }

        /// <returns>sent | failed | no_email | visitor_online | limit</returns>
        private async Task<string> NotifyByEmailAsync(
            ChatConversationRecord record, string message, DateTime now, CancellationToken cancellationToken)
        {
            // Si el visitante tiene la página abierta, la respuesta le llega al chat.
            if (_presence.IsVisitorOnline(record.PublicId))
                return "visitor_online";
            if (string.IsNullOrWhiteSpace(record.VisitorEmail))
                return "no_email";
            if (await _dao.CountEmailsSentSinceAsync(record.Id, now.AddDays(-1)) >= MaxEmailsPerConversationPerDay)
                return "limit";

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(EmailTimeout);
            try
            {
                bool sent = await _emailNotifier.SendReplyAsync(
                    record.VisitorEmail, record.VisitorName, message, timeout.Token);
                return sent ? "sent" : "failed";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo avisar por email la respuesta de la conversación {ConversationId}.", record.Id);
                return "failed";
            }
        }
    }
}
