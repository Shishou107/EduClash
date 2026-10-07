using System;
using api_Outline_management.Entities.Auth;

namespace api_Outline_management.Entities.Audit;

public class AdjustmentTransaction
{
    public long TransactionId { get; set; }
    public Guid AdminUserId { get; set; }
    public Guid TargetUserId { get; set; }
    public string FieldAdjusted { get; set; } = string.Empty; // 'Rating' | 'XP'
    public int OldValue { get; set; }
    public int NewValue { get; set; }
    public int Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual User AdminUser { get; set; } = null!;
    public virtual User TargetUser { get; set; } = null!;
}

public class AuditLog
{
    public long LogId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty; // 'INSERT' | 'UPDATE' | 'DELETE'
    public string RecordId { get; set; } = string.Empty;
    public Guid? ChangedByUserId { get; set; }
    public string? OldDataJson { get; set; }
    public string? NewDataJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
