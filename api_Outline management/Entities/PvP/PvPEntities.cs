using System;
using System.Collections.Generic;
using api_Outline_management.Entities.Auth;
using api_Outline_management.Entities.Quiz;

namespace api_Outline_management.Entities.PvP;

public class Match
{
    public Guid MatchId { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public string GameMode { get; set; } = "Random"; // 'Random' | 'Challenge'
    public string Status { get; set; } = "Pending"; // 'Pending' | 'Active' | 'Finished' | 'Forfeited' | 'Cancelled'
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = null!;

    public virtual api_Outline_management.Entities.Quiz.Quiz Quiz { get; set; } = null!;
    public virtual ICollection<MatchPlayer> Players { get; set; } = new List<MatchPlayer>();
}

public class MatchPlayer
{
    public long MatchPlayerId { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public int InitialRating { get; set; }
    public int FinalScore { get; set; }
    public int TotalTimeMs { get; set; }
    public int RatingDelta { get; set; }
    public int FinalRating { get; set; }
    public int XpEarned { get; set; }
    public bool IsWinner { get; set; }
    public bool IsForfeit { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual Match Match { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ICollection<MatchPlayerAnswer> Answers { get; set; } = new List<MatchPlayerAnswer>();
}

public class MatchPlayerAnswer
{
    public long Id { get; set; }
    public long MatchPlayerId { get; set; }
    public Guid QuestionId { get; set; }
    public string? SelectedAnswerIdsJson { get; set; }
    public string? TextResponse { get; set; }
    public bool IsCorrect { get; set; }
    public int ResponseTimeMs { get; set; }
    public int PointsAwarded { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual MatchPlayer MatchPlayer { get; set; } = null!;
    public virtual QuizQuestion Question { get; set; } = null!;
}

public class Achievement
{
    public int AchievementId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public int XpBonus { get; set; } = 50;
    public string Category { get; set; } = "PvP"; // 'PvP' | 'Content' | 'Streak'

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}

public class UserAchievement
{
    public Guid UserId { get; set; }
    public int AchievementId { get; set; }
    public DateTimeOffset EarnedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual User User { get; set; } = null!;
    public virtual Achievement Achievement { get; set; } = null!;
}
