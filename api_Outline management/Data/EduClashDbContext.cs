using Microsoft.EntityFrameworkCore;
using api_Outline_management.Entities.Auth;
using api_Outline_management.Entities.Core;
using api_Outline_management.Entities.Quiz;
using api_Outline_management.Entities.PvP;
using api_Outline_management.Entities.Audit;

namespace api_Outline_management.Data;

public class EduClashDbContext : DbContext
{
    public EduClashDbContext(DbContextOptions<EduClashDbContext> options) : base(options)
    {
    }

    // 1. Auth Schema
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CoinTransaction> CoinTransactions => Set<CoinTransaction>();

    // 2. Core Schema
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Content> Contents => Set<Content>();
    public DbSet<ContentTag> ContentTags => Set<ContentTag>();
    public DbSet<UserStar> UserStars => Set<UserStar>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Report> Reports => Set<Report>();

    // 3. Quiz Schema
    public DbSet<StudyOutline> StudyOutlines => Set<StudyOutline>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuestionAnswer> QuestionAnswers => Set<QuestionAnswer>();

    // 4. PvP Schema
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<MatchPlayerAnswer> MatchPlayerAnswers => Set<MatchPlayerAnswer>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();

    // 5. Audit Schema
    public DbSet<AdjustmentTransaction> AdjustmentTransactions => Set<AdjustmentTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ==========================================
        // 1. AUTH SCHEMA MAPPINGS
        // ==========================================
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles", "auth");
            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.HasIndex(e => e.RoleName).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", "auth");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.SecurityStamp).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => e.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles", "auth");
            entity.HasKey(e => new { e.UserId, e.RoleId });
            entity.HasOne(e => e.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("Profiles", "auth");
            entity.HasKey(e => e.ProfileId);
            entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AvatarUrl).HasMaxLength(1000);
            entity.Property(e => e.Bio).HasMaxLength(500);
            entity.Property(e => e.RankTier).HasMaxLength(50).IsRequired().HasDefaultValue("Đồng");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.User)
                  .WithOne(u => u.Profile)
                  .HasForeignKey<Profile>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens", "auth");
            entity.HasKey(e => e.TokenId);
            entity.Property(e => e.TokenHash).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CoinTransaction>(entity =>
        {
            entity.ToTable("CoinTransactions", "auth");
            entity.HasKey(e => e.TransactionId);
            entity.Property(e => e.TransactionType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // 2. CORE SCHEMA MAPPINGS
        // ==========================================
        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("Subjects", "core");
            entity.HasKey(e => e.SubjectId);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.NormalizedCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => e.NormalizedCode).IsUnique();
            entity.HasOne(e => e.ProposedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ProposedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tags", "core");
            entity.HasKey(e => e.TagId);
            entity.Property(e => e.TagName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.NormalizedTagName).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.NormalizedTagName).IsUnique();
        });

        modelBuilder.Entity<Content>(entity =>
        {
            entity.ToTable("Contents", "core");
            entity.HasKey(e => e.ContentId);
            entity.Property(e => e.ContentType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(350).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Visibility).HasMaxLength(20).IsRequired().HasDefaultValue("Public");
            entity.Property(e => e.DifficultyLevel).HasMaxLength(20).IsRequired().HasDefaultValue("Medium");
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasOne(e => e.Owner)
                  .WithMany()
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Subject)
                  .WithMany(s => s.Contents)
                  .HasForeignKey(e => e.SubjectId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ForkedFrom)
                  .WithMany(c => c.ForkedCopies)
                  .HasForeignKey(e => e.ForkedFromId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContentTag>(entity =>
        {
            entity.ToTable("ContentTags", "core");
            entity.HasKey(e => new { e.ContentId, e.TagId });
            entity.HasOne(e => e.Content)
                  .WithMany(c => c.ContentTags)
                  .HasForeignKey(e => e.ContentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tag)
                  .WithMany(t => t.ContentTags)
                  .HasForeignKey(e => e.TagId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserStar>(entity =>
        {
            entity.ToTable("UserStars", "core");
            entity.HasKey(e => new { e.UserId, e.ContentId });
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Content)
                  .WithMany(c => c.UserStars)
                  .HasForeignKey(e => e.ContentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("Comments", "core");
            entity.HasKey(e => e.CommentId);
            entity.Property(e => e.Message).HasMaxLength(2000).IsRequired();
            entity.HasOne(e => e.Content)
                  .WithMany(c => c.Comments)
                  .HasForeignKey(e => e.ContentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentComment)
                  .WithMany(c => c.Replies)
                  .HasForeignKey(e => e.ParentCommentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.ToTable("Reports", "core");
            entity.HasKey(e => e.ReportId);
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired().HasDefaultValue("Pending");
            entity.Property(e => e.AdminNote).HasMaxLength(1000);

            entity.HasOne(e => e.ReporterUser)
                  .WithMany()
                  .HasForeignKey(e => e.ReporterUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetContent)
                  .WithMany()
                  .HasForeignKey(e => e.TargetContentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TargetUser)
                  .WithMany()
                  .HasForeignKey(e => e.TargetUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ResolvedByAdmin)
                  .WithMany()
                  .HasForeignKey(e => e.ResolvedByAdminId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ==========================================
        // 3. QUIZ SCHEMA MAPPINGS
        // ==========================================
        modelBuilder.Entity<StudyOutline>(entity =>
        {
            entity.ToTable("StudyOutlines", "quiz");
            entity.HasKey(e => e.ContentId);
            entity.Property(e => e.RichTextContent).IsRequired();
            entity.Property(e => e.AttachedFileUrl).HasMaxLength(1000);
            entity.HasOne(e => e.Content)
                  .WithOne(c => c.StudyOutline)
                  .HasForeignKey<StudyOutline>(e => e.ContentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Quiz>(entity =>
        {
            entity.ToTable("Quizzes", "quiz");
            entity.HasKey(e => e.ContentId);
            entity.HasOne(e => e.Content)
                  .WithOne(c => c.Quiz)
                  .HasForeignKey<Quiz>(e => e.ContentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuizQuestion>(entity =>
        {
            entity.ToTable("QuizQuestions", "quiz");
            entity.HasKey(e => e.QuestionId);
            entity.Property(e => e.QuestionText).IsRequired();
            entity.Property(e => e.QuestionType).HasMaxLength(50).IsRequired().HasDefaultValue("SingleChoice");
            entity.Property(e => e.MediaUrl).HasMaxLength(1000);
            entity.HasOne(e => e.Quiz)
                  .WithMany(q => q.Questions)
                  .HasForeignKey(e => e.QuizId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionAnswer>(entity =>
        {
            entity.ToTable("QuestionAnswers", "quiz");
            entity.HasKey(e => e.AnswerId);
            entity.Property(e => e.AnswerText).IsRequired();
            entity.HasOne(e => e.Question)
                  .WithMany(q => q.Answers)
                  .HasForeignKey(e => e.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // 4. PVP SCHEMA MAPPINGS
        // ==========================================
        modelBuilder.Entity<Match>(entity =>
        {
            entity.ToTable("Matches", "pvp");
            entity.HasKey(e => e.MatchId);
            entity.Property(e => e.GameMode).HasMaxLength(50).IsRequired().HasDefaultValue("Random");
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired().HasDefaultValue("Pending");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.Quiz)
                  .WithMany(q => q.Matches)
                  .HasForeignKey(e => e.QuizId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchPlayer>(entity =>
        {
            entity.ToTable("MatchPlayers", "pvp");
            entity.HasKey(e => e.MatchPlayerId);
            entity.HasIndex(e => new { e.MatchId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Match)
                  .WithMany(m => m.Players)
                  .HasForeignKey(e => e.MatchId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchPlayerAnswer>(entity =>
        {
            entity.ToTable("MatchPlayerAnswers", "pvp");
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.MatchPlayer)
                  .WithMany(p => p.Answers)
                  .HasForeignKey(e => e.MatchPlayerId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Question)
                  .WithMany()
                  .HasForeignKey(e => e.QuestionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.ToTable("Achievements", "pvp");
            entity.HasKey(e => e.AchievementId);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(300).IsRequired();
            entity.Property(e => e.IconUrl).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired().HasDefaultValue("PvP");
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.ToTable("UserAchievements", "pvp");
            entity.HasKey(e => new { e.UserId, e.AchievementId });
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Achievement)
                  .WithMany(a => a.UserAchievements)
                  .HasForeignKey(e => e.AchievementId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // 5. AUDIT SCHEMA MAPPINGS
        // ==========================================
        modelBuilder.Entity<AdjustmentTransaction>(entity =>
        {
            entity.ToTable("AdjustmentTransactions", "audit");
            entity.HasKey(e => e.TransactionId);
            entity.Property(e => e.FieldAdjusted).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.AdminUser)
                  .WithMany()
                  .HasForeignKey(e => e.AdminUserId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TargetUser)
                  .WithMany()
                  .HasForeignKey(e => e.TargetUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs", "audit");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.TableName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ActionType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.RecordId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(50);
        });
    }
}
