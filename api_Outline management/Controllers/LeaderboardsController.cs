using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api_Outline_management.Data;

namespace api_Outline_management.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class LeaderboardsController : ControllerBase
{
    private readonly EduClashDbContext _context;

    public LeaderboardsController(EduClashDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy top đấu thủ xếp hạng theo Elo Rating (UC09)
    /// Tối ưu bằng Covering Index IX_Profiles_Rating_Leaderboard
    /// </summary>
    [HttpGet("rating")]
    public async Task<IActionResult> GetTopElo([FromQuery] int limit = 20)
    {
        if (limit is < 1 or > 100) limit = 20;

        var leaderboard = await _context.Profiles
            .AsNoTracking()
            .Where(p => !p.User.IsDeleted && p.User.IsActive)
            .OrderByDescending(p => p.Rating)
            .ThenByDescending(p => p.Wins)
            .Take(limit)
            .Select(p => new
            {
                p.UserId,
                p.DisplayName,
                p.AvatarUrl,
                p.Rating,
                p.RankTier,
                p.Level,
                p.Wins,
                p.Losses,
                p.CurrentStreak,
                WinRate = (p.Wins + p.Losses) > 0 ? Math.Round((double)p.Wins / (p.Wins + p.Losses) * 100, 1) : 0
            })
            .ToListAsync();

        return Ok(leaderboard);
    }

    /// <summary>
    /// Lấy top người dùng theo kinh nghiệm (CurrentXP)
    /// </summary>
    [HttpGet("xp")]
    public async Task<IActionResult> GetTopXp([FromQuery] int limit = 20)
    {
        if (limit is < 1 or > 100) limit = 20;

        var leaderboard = await _context.Profiles
            .AsNoTracking()
            .Where(p => !p.User.IsDeleted && p.User.IsActive)
            .OrderByDescending(p => p.CurrentXP)
            .Take(limit)
            .Select(p => new
            {
                p.UserId,
                p.DisplayName,
                p.AvatarUrl,
                p.CurrentXP,
                p.Level,
                p.Rating,
                p.RankTier
            })
            .ToListAsync();

        return Ok(leaderboard);
    }
}
