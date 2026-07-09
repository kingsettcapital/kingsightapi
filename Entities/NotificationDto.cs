namespace kingsightapi.Entities
{
    public sealed class NotificationDto
    {
        public long NotificationId { get; init; }
        public string NotificationType { get; init; } = string.Empty;
        public string Notice { get; init; } = string.Empty;
        public bool IsRead { get; init; }
        public string UpdatedBy { get; init; } = string.Empty;
        public DateTime UpdatedDate { get; init; }
    }

    public sealed class NotificationMarkReadRequest
    {
        public List<long> NotificationIds { get; init; } = [];
    }
}
