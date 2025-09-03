namespace MultiTenacy.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string TableName { get; set; } = null!;
        public string Action { get; set; } = null!; // Insert, Update, Delete
        public string KeyValues { get; set; } = null!;
        public string OldValues { get; set; } = null!;
        public string NewValues { get; set; } = null!;
        public string TenantId { get; set; } = null!;
        public string? UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
