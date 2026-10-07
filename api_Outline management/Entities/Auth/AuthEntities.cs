using System;
using System.Collections.Generic;

namespace api_Outline_management.Entities.Auth;

public class User
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();
    public bool IsEmailConfirmed { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual Profile? Profile { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class UserRole
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual User User { get; set; } = null!;
    public virtual Role Role { get; set; } = null!;
}

public class Profile
{
    public Guid ProfileId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public long CurrentXP { get; set; }
    public int Level { get; set; } = 1;
    public int Rating { get; set; } = 1000; // Elo xuất phát
    public string RankTier { get; set; } = "Đồng";
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public int Coins { get; set; } = 50; // Xu sử dụng dịch vụ AI / nhận từ PvP
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}

public class RefreshToken
{
    public long TokenId { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ReplacedByTokenHash { get; set; }

    public virtual User User { get; set; } = null!;
}

public class CoinTransaction
{
    public long TransactionId { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public string TransactionType { get; set; } = "PvPReward"; // 'PvPReward' | 'AiQuizGeneration' | 'Deposit'
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual User User { get; set; } = null!;
}
