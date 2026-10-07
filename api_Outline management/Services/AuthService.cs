using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using api_Outline_management.Data;
using api_Outline_management.DTOs;
using api_Outline_management.Entities.Auth;

namespace api_Outline_management.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto dto);
    Task<UserProfileDto?> GetProfileByUserIdAsync(Guid userId);
}

public class AuthService : IAuthService
{
    private readonly EduClashDbContext _context;
    private readonly IConfiguration _config;

    public AuthService(EduClashDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    public async Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToUpperInvariant();
        if (await _context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail))
        {
            return null; // Email đã tồn tại
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = dto.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = HashPassword(dto.Password),
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        var profile = new Profile
        {
            ProfileId = Guid.NewGuid(),
            UserId = user.UserId,
            DisplayName = dto.DisplayName.Trim(),
            Rating = 1000,
            RankTier = "Đồng",
            Level = 1,
            CurrentXP = 0
        };

        // Gán Role mặc định: 'User' và 'Player'
        var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
        var playerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Player");

        _context.Users.Add(user);
        _context.Profiles.Add(profile);

        if (userRole != null)
        {
            _context.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = userRole.RoleId });
        }
        if (playerRole != null)
        {
            _context.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = playerRole.RoleId });
        }

        await _context.SaveChangesAsync();

        var roles = new List<string> { "User", "Player" };
        var token = GenerateJwtToken(user, profile, roles);

        return new AuthResponseDto(
            AccessToken: token,
            RefreshToken: Guid.NewGuid().ToString(),
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(Convert.ToDouble(_config["JwtSettings:DurationInMinutes"] ?? "1440")),
            User: MapToProfileDto(user, profile, roles)
        );
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToUpperInvariant();
        var user = await _context.Users
            .Include(u => u.Profile)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail && !u.IsDeleted);

        if (user == null || !VerifyPassword(dto.Password, user.PasswordHash))
        {
            return null;
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        var roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();
        if (roles.Count == 0) roles.Add("User");

        var token = GenerateJwtToken(user, user.Profile!, roles);

        return new AuthResponseDto(
            AccessToken: token,
            RefreshToken: Guid.NewGuid().ToString(),
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(Convert.ToDouble(_config["JwtSettings:DurationInMinutes"] ?? "1440")),
            User: MapToProfileDto(user, user.Profile!, roles)
        );
    }

    public async Task<UserProfileDto?> GetProfileByUserIdAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.Profile)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted);

        if (user == null || user.Profile == null) return null;

        var roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();
        return MapToProfileDto(user, user.Profile, roles);
    }

    private string GenerateJwtToken(User user, Profile profile, List<string> roles)
    {
        var secretKey = _config["JwtSettings:SecretKey"] ?? "EduClash_Super_Secret_Key_For_JWT_Authentication_2026_Minimum_32_Chars!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, profile.DisplayName),
            new("Rating", profile.Rating.ToString()),
            new("RankTier", profile.RankTier),
            new("Level", profile.Level.ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var durationMinutes = Convert.ToDouble(_config["JwtSettings:DurationInMinutes"] ?? "1440");
        var token = new JwtSecurityToken(
            issuer: _config["JwtSettings:Issuer"] ?? "EduClashAPI",
            audience: _config["JwtSettings:Audience"] ?? "EduClashClient",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(durationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserProfileDto MapToProfileDto(User user, Profile profile, List<string> roles)
    {
        return new UserProfileDto(
            UserId: user.UserId,
            Email: user.Email,
            DisplayName: profile.DisplayName,
            AvatarUrl: profile.AvatarUrl,
            Bio: profile.Bio,
            CurrentXP: profile.CurrentXP,
            Level: profile.Level,
            Rating: profile.Rating,
            RankTier: profile.RankTier,
            Wins: profile.Wins,
            Losses: profile.Losses,
            CurrentStreak: profile.CurrentStreak,
            BestStreak: profile.BestStreak,
            Coins: profile.Coins,
            Roles: roles
        );
    }

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);

        byte[] combined = new byte[48];
        Buffer.BlockCopy(salt, 0, combined, 0, 16);
        Buffer.BlockCopy(hash, 0, combined, 16, 32);
        return Convert.ToBase64String(combined);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            byte[] combined = Convert.FromBase64String(storedHash);
            if (combined.Length != 48) return false;

            byte[] salt = new byte[16];
            byte[] originalHash = new byte[32];
            Buffer.BlockCopy(combined, 0, salt, 0, 16);
            Buffer.BlockCopy(combined, 16, originalHash, 0, 32);

            byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations: 100000,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: 32);

            return CryptographicOperations.FixedTimeEquals(originalHash, computedHash);
        }
        catch
        {
            return false;
        }
    }
}
