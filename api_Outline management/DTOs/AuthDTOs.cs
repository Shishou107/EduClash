using System;
using System.ComponentModel.DataAnnotations;

namespace api_Outline_management.DTOs;

public record RegisterRequestDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password,
    [Required, MinLength(2), MaxLength(50)] string DisplayName
);

public record LoginRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserProfileDto User
);

public record UserProfileDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    long CurrentXP,
    int Level,
    int Rating,
    string RankTier,
    int Wins,
    int Losses,
    int CurrentStreak,
    int BestStreak,
    int Coins,
    List<string> Roles
);
