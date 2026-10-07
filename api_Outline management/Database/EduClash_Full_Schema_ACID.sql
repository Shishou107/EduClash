-- ====================================================================================================
-- DỰ ÁN: EDUCLASH (STUDYARENA) - HỆ THỐNG QUẢN LÝ ĐỀ CƯƠNG & ĐẤU TRƯỜNG TRI THỨC GAMIFICATION
-- FILE: EduClash_Full_Schema_ACID.sql
-- MỤC TIÊU: 
--   1. Chuẩn ACID (Atomicity, Consistency, Isolation, Durability)
--   2. Chuẩn hóa 3NF (Data Modeling) & Phản chuẩn hóa có kiểm soát (Denormalization counters)
--   3. Tối ưu hiệu năng: Clustered/Non-clustered Index, Covering Index, Filtered Index, Keyset Pagination
--   4. Bảo mật: Chống SQL Injection, Principle of Least Privilege, Hash Storage, Audit Trails
--   5. Khả năng mở rộng & Chịu tải: RCSI (Read Committed Snapshot Isolation), Row locks, Partitioning ready
--   6. Khả năng bảo trì: Script Idempotent (chạy nhiều lần không lỗi), Stored Procedures & Views chuẩn mực
--   7. Giám sát & Dự phòng: Index Defragmentation, Statistics update, Backup & Recovery templates
-- HỆ QUẢN TRỊ CSDL: Microsoft SQL Server 2019 / 2022 / Azure SQL
-- ====================================================================================================

USE master;
GO

-- ====================================================================================================
-- PHẦN 1: CẤU HÌNH DATABASE & CƠ CHẾ CÔ LẬP GIAO DỊCH (TRANSACTION ISOLATION & CONCURRENCY)
-- ====================================================================================================
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'EduClashDB')
BEGIN
    CREATE DATABASE EduClashDB
    COLLATE Vietnamese_100_CI_AS_SC_UTF8; -- Hỗ trợ tiếng Việt và chuẩn UTF-8 tối ưu
    PRINT N'[OK] Đã khởi tạo cơ sở dữ liệu EduClashDB.';
END
GO

USE EduClashDB;
GO

-- Bật Read Committed Snapshot Isolation (RCSI) để chống tắc nghẽn đọc-ghi (Deadlock prevention)
-- Giúp các câu lệnh SELECT không block UPDATE/INSERT và ngược lại, cực kỳ quan trọng cho SignalR PvP
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'EduClashDB') = 0
BEGIN
    ALTER DATABASE EduClashDB SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
    ALTER DATABASE EduClashDB SET ALLOW_SNAPSHOT_ISOLATION ON;
    PRINT N'[OK] Đã kích hoạt Snapshot Isolation & RCSI cho EduClashDB.';
END
GO

-- ====================================================================================================
-- PHẦN 2: TỔ CHỨC CẤU TRÚC SCHEMA THEO DOMAIN (SEPARATION OF CONCERNS)
-- ====================================================================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'auth')
    EXEC('CREATE SCHEMA [auth];');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'core')
    EXEC('CREATE SCHEMA [core];');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'quiz')
    EXEC('CREATE SCHEMA [quiz];');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'pvp')
    EXEC('CREATE SCHEMA [pvp];');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'audit')
    EXEC('CREATE SCHEMA [audit];');
GO
PRINT N'[OK] Đã cấu hình các Schema: auth, core, quiz, pvp, audit.';
GO

-- ====================================================================================================
-- PHẦN 3: BẢNG DỮ LIỆU ĐẠT CHUẨN 3NF (DATA MODELING & CONSTRAINTS)
-- ====================================================================================================

------------------------------------------------------------------------------------------------------
-- 3.1. SCHEMA: [auth] - ĐỊNH DANH & NGƯỜI DÙNG
------------------------------------------------------------------------------------------------------

-- Bảng Roles
IF OBJECT_ID(N'auth.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE auth.Roles (
        RoleId INT IDENTITY(1,1) NOT NULL,
        RoleName NVARCHAR(50) NOT NULL,
        Description NVARCHAR(255) NULL,
        CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (RoleId),
        CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName),
        CONSTRAINT CK_Roles_RoleName CHECK (RoleName IN ('Guest', 'User', 'Creator', 'Player', 'Admin'))
    );
    PRINT N'[OK] Tạo bảng auth.Roles.';
END
GO

-- Bảng Users
IF OBJECT_ID(N'auth.Users', N'U') IS NULL
BEGIN
    CREATE TABLE auth.Users (
        UserId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Email NVARCHAR(256) NOT NULL,
        NormalizedEmail NVARCHAR(256) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        SecurityStamp NVARCHAR(100) NOT NULL DEFAULT NEWID(),
        IsEmailConfirmed BIT NOT NULL DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        LastLoginAt DATETIMEOFFSET NULL,
        RowVersion ROWVERSION NOT NULL, -- Optimistic concurrency token
        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (UserId)
    );
    PRINT N'[OK] Tạo bảng auth.Users.';
END
GO

-- Bảng liên kết User - Role (N - N)
IF OBJECT_ID(N'auth.UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE auth.UserRoles (
        UserId UNIQUEIDENTIFIER NOT NULL,
        RoleId INT NOT NULL,
        AssignedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
        CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES auth.Users(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES auth.Roles(RoleId) ON DELETE CASCADE
    );
    PRINT N'[OK] Tạo bảng auth.UserRoles.';
END
GO

-- Bảng Profiles (Quan hệ 1 - 1 với Users)
IF OBJECT_ID(N'auth.Profiles', N'U') IS NULL
BEGIN
    CREATE TABLE auth.Profiles (
        ProfileId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(100) NOT NULL,
        AvatarUrl NVARCHAR(1000) NULL,
        Bio NVARCHAR(500) NULL,
        CurrentXP BIGINT NOT NULL DEFAULT 0,
        Level INT NOT NULL DEFAULT 1,
        Rating INT NOT NULL DEFAULT 1000, -- Mốc điểm khởi đầu (Đồng)
        RankTier NVARCHAR(50) NOT NULL DEFAULT N'Đồng',
        Wins INT NOT NULL DEFAULT 0,
        Losses INT NOT NULL DEFAULT 0,
        CurrentStreak INT NOT NULL DEFAULT 0,
        BestStreak INT NOT NULL DEFAULT 0,
        UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT PK_Profiles PRIMARY KEY CLUSTERED (ProfileId),
        CONSTRAINT UQ_Profiles_UserId UNIQUE (UserId),
        CONSTRAINT FK_Profiles_Users FOREIGN KEY (UserId) REFERENCES auth.Users(UserId) ON DELETE CASCADE,
        CONSTRAINT CK_Profiles_CurrentXP CHECK (CurrentXP >= 0),
        CONSTRAINT CK_Profiles_Rating CHECK (Rating >= 0),
        CONSTRAINT CK_Profiles_RankTier CHECK (RankTier IN (N'Tân Binh', N'Đồng', N'Bạc', N'Vàng', N'Bạch Kim', N'Kim Cương'))
    );
    PRINT N'[OK] Tạo bảng auth.Profiles.';
END
GO

-- Bảng Refresh Tokens (Quản lý phiên đăng nhập an toàn)
IF OBJECT_ID(N'auth.RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE auth.RefreshTokens (
        TokenId BIGINT IDENTITY(1,1) NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        TokenHash NVARCHAR(500) NOT NULL,
        ExpiresAt DATETIMEOFFSET NOT NULL,
        IsRevoked BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        ReplacedByTokenHash NVARCHAR(500) NULL,
        CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (TokenId),
        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES auth.Users(UserId) ON DELETE CASCADE
    );
    PRINT N'[OK] Tạo bảng auth.RefreshTokens.';
END
GO

------------------------------------------------------------------------------------------------------
-- 3.2. SCHEMA: [core] - DANH MỤC, NỘI DUNG GỐC & TƯƠNG TÁC XÃ HỘI
------------------------------------------------------------------------------------------------------

-- Bảng Subjects (Môn học)
IF OBJECT_ID(N'core.Subjects', N'U') IS NULL
BEGIN
    CREATE TABLE core.Subjects (
        SubjectId INT IDENTITY(1,1) NOT NULL,
        Code NVARCHAR(50) NOT NULL,
        NormalizedCode NVARCHAR(50) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        Description NVARCHAR(500) NULL,
        IsOfficial BIT NOT NULL DEFAULT 1,
        ProposedByUserId UNIQUEIDENTIFIER NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Subjects PRIMARY KEY CLUSTERED (SubjectId),
        CONSTRAINT UQ_Subjects_NormalizedCode UNIQUE (NormalizedCode),
        CONSTRAINT FK_Subjects_ProposedUser FOREIGN KEY (ProposedByUserId) REFERENCES auth.Users(UserId) ON DELETE SET NULL
    );
    PRINT N'[OK] Tạo bảng core.Subjects.';
END
GO

-- Bảng Tags (Phẳng hóa linh hoạt: #HUIT, #KHTN, #OOP, #Database)
IF OBJECT_ID(N'core.Tags', N'U') IS NULL
BEGIN
    CREATE TABLE core.Tags (
        TagId INT IDENTITY(1,1) NOT NULL,
        TagName NVARCHAR(50) NOT NULL,
        NormalizedTagName NVARCHAR(50) NOT NULL,
        UsageCount INT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Tags PRIMARY KEY CLUSTERED (TagId),
        CONSTRAINT UQ_Tags_NormalizedName UNIQUE (NormalizedTagName)
    );
    PRINT N'[OK] Tạo bảng core.Tags.';
END
GO

-- Bảng Contents (Thực thể cha trừu tượng - Table Per Type Pattern)
IF OBJECT_ID(N'core.Contents', N'U') IS NULL
BEGIN
    CREATE TABLE core.Contents (
        ContentId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        OwnerId UNIQUEIDENTIFIER NOT NULL,
        SubjectId INT NOT NULL,
        ContentType NVARCHAR(50) NOT NULL, -- 'StudyOutline' | 'Quiz'
        Title NVARCHAR(300) NOT NULL,
        Slug NVARCHAR(350) NOT NULL,
        Description NVARCHAR(1000) NULL,
        Visibility NVARCHAR(20) NOT NULL DEFAULT 'Public', -- 'Public' | 'Private' | 'Unlisted'
        DifficultyLevel NVARCHAR(20) NOT NULL DEFAULT 'Medium', -- 'Easy' | 'Medium' | 'Hard'
        ForkedFromId UNIQUEIDENTIFIER NULL, -- Cơ chế dẫn xuất (Fork)
        
        -- Các cột đếm phản chuẩn hóa có kiểm soát (Denormalized Counters)
        StarCount INT NOT NULL DEFAULT 0,
        CommentCount INT NOT NULL DEFAULT 0,
        ViewCount INT NOT NULL DEFAULT 0,
        ForkCount INT NOT NULL DEFAULT 0,

        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT PK_Contents PRIMARY KEY CLUSTERED (ContentId),
        CONSTRAINT FK_Contents_Owner FOREIGN KEY (OwnerId) REFERENCES auth.Users(UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_Contents_Subject FOREIGN KEY (SubjectId) REFERENCES core.Subjects(SubjectId) ON DELETE NO ACTION,
        CONSTRAINT FK_Contents_ForkedFrom FOREIGN KEY (ForkedFromId) REFERENCES core.Contents(ContentId) ON DELETE NO ACTION,
        CONSTRAINT CK_Contents_ContentType CHECK (ContentType IN ('StudyOutline', 'Quiz')),
        CONSTRAINT CK_Contents_Visibility CHECK (Visibility IN ('Public', 'Private', 'Unlisted')),
        CONSTRAINT CK_Contents_Difficulty CHECK (DifficultyLevel IN ('Easy', 'Medium', 'Hard')),
        CONSTRAINT CK_Contents_Counters CHECK (StarCount >= 0 AND CommentCount >= 0 AND ViewCount >= 0 AND ForkCount >= 0)
    );
    PRINT N'[OK] Tạo bảng core.Contents.';
END
GO

-- Bảng liên kết Content - Tags (N - N)
IF OBJECT_ID(N'core.ContentTags', N'U') IS NULL
BEGIN
    CREATE TABLE core.ContentTags (
        ContentId UNIQUEIDENTIFIER NOT NULL,
        TagId INT NOT NULL,
        CONSTRAINT PK_ContentTags PRIMARY KEY CLUSTERED (ContentId, TagId),
        CONSTRAINT FK_ContentTags_Contents FOREIGN KEY (ContentId) REFERENCES core.Contents(ContentId) ON DELETE CASCADE,
        CONSTRAINT FK_ContentTags_Tags FOREIGN KEY (TagId) REFERENCES core.Tags(TagId) ON DELETE CASCADE
    );
    PRINT N'[OK] Tạo bảng core.ContentTags.';
END
GO

-- Bảng UserStars (Đánh giá yêu thích - Chống vote trùng lặp)
IF OBJECT_ID(N'core.UserStars', N'U') IS NULL
BEGIN
    CREATE TABLE core.UserStars (
        UserId UNIQUEIDENTIFIER NOT NULL,
        ContentId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_UserStars PRIMARY KEY CLUSTERED (UserId, ContentId),
        CONSTRAINT FK_UserStars_Users FOREIGN KEY (UserId) REFERENCES auth.Users(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_UserStars_Contents FOREIGN KEY (ContentId) REFERENCES core.Contents(ContentId) ON DELETE CASCADE
    );
    PRINT N'[OK] Tạo bảng core.UserStars.';
END
GO

-- Bảng Comments (Mô hình 2 cấp: Root Comment -> Direct Replies)
IF OBJECT_ID(N'core.Comments', N'U') IS NULL
BEGIN
    CREATE TABLE core.Comments (
        CommentId BIGINT IDENTITY(1,1) NOT NULL,
        ContentId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        ParentCommentId BIGINT NULL, -- Tự tham chiếu cấp 2
        Message NVARCHAR(2000) NOT NULL,
        IsHiddenByAdmin BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NULL,
        CONSTRAINT PK_Comments PRIMARY KEY CLUSTERED (CommentId),
        CONSTRAINT FK_Comments_Contents FOREIGN KEY (ContentId) REFERENCES core.Contents(ContentId) ON DELETE CASCADE,
        CONSTRAINT FK_Comments_Users FOREIGN KEY (UserId) REFERENCES auth.Users(UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_Comments_Parent FOREIGN KEY (ParentCommentId) REFERENCES core.Comments(CommentId) ON DELETE NO ACTION
    );
    PRINT N'[OK] Tạo bảng core.Comments.';
END
GO

-- Bảng Reports (Xử lý tố cáo vi phạm)
IF OBJECT_ID(N'core.Reports', N'U') IS NULL
BEGIN
    CREATE TABLE core.Reports (
        ReportId BIGINT IDENTITY(1,1) NOT NULL,
        ReporterUserId UNIQUEIDENTIFIER NOT NULL,
        TargetContentId UNIQUEIDENTIFIER NULL,
        TargetUserId UNIQUEIDENTIFIER NULL,
        Reason NVARCHAR(500) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        AdminNote NVARCHAR(1000) NULL,
        ResolvedByAdminId UNIQUEIDENTIFIER NULL,
        ResolvedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Reports PRIMARY KEY CLUSTERED (ReportId),
        CONSTRAINT FK_Reports_Reporter FOREIGN KEY (ReporterUserId) REFERENCES auth.Users(UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_Reports_Content FOREIGN KEY (TargetContentId) REFERENCES core.Contents(ContentId) ON DELETE SET NULL,
        CONSTRAINT FK_Reports_TargetUser FOREIGN KEY (TargetUserId) REFERENCES auth.Users(UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_Reports_Admin FOREIGN KEY (ResolvedByAdminId) REFERENCES auth.Users(UserId) ON DELETE NO ACTION,
        CONSTRAINT CK_Reports_Status CHECK (Status IN ('Pending', 'Resolved', 'Dismissed'))
    );
    PRINT N'[OK] Tạo bảng core.Reports.';
END
GO

------------------------------------------------------------------------------------------------------
-- 3.3. SCHEMA: [quiz] - STUDY OUTLINES & QUIZ ENGINE
------------------------------------------------------------------------------------------------------

-- Bảng StudyOutlines (1:1 với Contents)
IF OBJECT_ID(N'quiz.StudyOutlines', N'U') IS NULL
BEGIN
    CREATE TABLE quiz.StudyOutlines (
        ContentId UNIQUEIDENTIFIER NOT NULL,
        RichTextContent NVARCHAR(MAX) NOT NULL, -- Hỗ trợ Markdown / Rich-text
        AttachedFileUrl NVARCHAR(1000) NULL,    -- Đính kèm file PDF/DOCX
        AttachedFileSizeBytes BIGINT NULL,
        TableOfContentsJson NVARCHAR(MAX) NULL, -- Chỉ mục chương mục phục vụ navigate
        CONSTRAINT PK_StudyOutlines PRIMARY KEY CLUSTERED (ContentId),
        CONSTRAINT FK_StudyOutlines_Contents FOREIGN KEY (ContentId) REFERENCES core.Contents(ContentId) ON DELETE CASCADE
    );
    PRINT N'[OK] Tạo bảng quiz.StudyOutlines.';
END
GO

-- Bảng Quizzes (1:1 với Contents)
IF OBJECT_ID(N'quiz.Quizzes', N'U') IS NULL
BEGIN
    CREATE TABLE quiz.Quizzes (
        ContentId UNIQUEIDENTIFIER NOT NULL,
        TotalQuestions INT NOT NULL DEFAULT 0,
        TimePerQuestionSec INT NOT NULL DEFAULT 30,
        PassThresholdPercentage INT NOT NULL DEFAULT 70,
        AllowShuffleQuestions BIT NOT NULL DEFAULT 1,
        AllowShuffleAnswers BIT NOT NULL DEFAULT 1,
        IsPvPEnabled BIT NOT NULL DEFAULT 1,
        CONSTRAINT PK_Quizzes PRIMARY KEY CLUSTERED (ContentId),
        CONSTRAINT FK_Quizzes_Contents FOREIGN KEY (ContentId) REFERENCES core.Contents(ContentId) ON DELETE CASCADE,
        CONSTRAINT CK_Quizzes_Time CHECK (TimePerQuestionSec BETWEEN 5 AND 300),
        CONSTRAINT CK_Quizzes_Threshold CHECK (PassThresholdPercentage BETWEEN 1 AND 100)
    );
    PRINT N'[OK] Tạo bảng quiz.Quizzes.';
END
GO

-- Bảng QuizQuestions (Ngân hàng câu hỏi của Quiz)
IF OBJECT_ID(N'quiz.QuizQuestions', N'U') IS NULL
BEGIN
    CREATE TABLE quiz.QuizQuestions (
        QuestionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        QuizId UNIQUEIDENTIFIER NOT NULL,
        QuestionText NVARCHAR(MAX) NOT NULL,
        QuestionType NVARCHAR(50) NOT NULL DEFAULT 'SingleChoice', -- 'SingleChoice' | 'MultipleChoice' | 'Essay'
        Points INT NOT NULL DEFAULT 10,
        OrderIndex INT NOT NULL DEFAULT 1,
        Explanation NVARCHAR(MAX) NULL,
        MediaUrl NVARCHAR(1000) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_QuizQuestions PRIMARY KEY CLUSTERED (QuestionId),
        CONSTRAINT FK_QuizQuestions_Quizzes FOREIGN KEY (QuizId) REFERENCES quiz.Quizzes(ContentId) ON DELETE CASCADE,
        CONSTRAINT CK_QuizQuestions_Type CHECK (QuestionType IN ('SingleChoice', 'MultipleChoice', 'Essay', 'TrueFalse'))
    );
    PRINT N'[OK] Tạo bảng quiz.QuizQuestions.';
END
GO

-- Bảng QuestionAnswers (Các lựa chọn đáp án)
IF OBJECT_ID(N'quiz.QuestionAnswers', N'U') IS NULL
BEGIN
    CREATE TABLE quiz.QuestionAnswers (
        AnswerId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        QuestionId UNIQUEIDENTIFIER NOT NULL,
        AnswerText NVARCHAR(MAX) NOT NULL,
        IsCorrect BIT NOT NULL DEFAULT 0,
        OrderIndex INT NOT NULL DEFAULT 1,
        CONSTRAINT PK_QuestionAnswers PRIMARY KEY CLUSTERED (AnswerId),
        CONSTRAINT FK_QuestionAnswers_Questions FOREIGN KEY (QuestionId) REFERENCES quiz.QuizQuestions(QuestionId) ON DELETE CASCADE
    );
    PRINT N'[OK] Tạo bảng quiz.QuestionAnswers.';
END
GO

------------------------------------------------------------------------------------------------------
-- 3.4. SCHEMA: [pvp] - ĐẤU TRƯỜNG THỜI GIAN THỰC & GAMIFICATION
------------------------------------------------------------------------------------------------------

-- Bảng Matches (Phòng thi đấu trực tiếp)
IF OBJECT_ID(N'pvp.Matches', N'U') IS NULL
BEGIN
    CREATE TABLE pvp.Matches (
        MatchId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        QuizId UNIQUEIDENTIFIER NOT NULL,
        GameMode NVARCHAR(50) NOT NULL DEFAULT 'Random', -- 'Random' | 'Challenge'
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',   -- 'Pending' | 'Active' | 'Finished' | 'Forfeited' | 'Cancelled'
        StartedAt DATETIMEOFFSET NULL,
        FinishedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT PK_Matches PRIMARY KEY CLUSTERED (MatchId),
        CONSTRAINT FK_Matches_Quizzes FOREIGN KEY (QuizId) REFERENCES quiz.Quizzes(ContentId) ON DELETE NO ACTION,
        CONSTRAINT CK_Matches_Status CHECK (Status IN ('Pending', 'Active', 'Finished', 'Forfeited', 'Cancelled'))
    );
    PRINT N'[OK] Tạo bảng pvp.Matches.';
END
GO

-- Bảng MatchPlayers (Người tham gia đấu)
IF OBJECT_ID(N'pvp.MatchPlayers', N'U') IS NULL
BEGIN
    CREATE TABLE pvp.MatchPlayers (
        MatchPlayerId BIGINT IDENTITY(1,1) NOT NULL,
        MatchId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        InitialRating INT NOT NULL,
        FinalScore INT NOT NULL DEFAULT 0,
        TotalTimeMs INT NOT NULL DEFAULT 0,
        RatingDelta INT NOT NULL DEFAULT 0,
        FinalRating INT NOT NULL DEFAULT 0,
        XpEarned INT NOT NULL DEFAULT 0,
        IsWinner BIT NOT NULL DEFAULT 0,
        IsForfeit BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_MatchPlayers PRIMARY KEY CLUSTERED (MatchPlayerId),
        CONSTRAINT UQ_MatchPlayers_Match_User UNIQUE (MatchId, UserId),
        CONSTRAINT FK_MatchPlayers_Matches FOREIGN KEY (MatchId) REFERENCES pvp.Matches(MatchId) ON DELETE CASCADE,
        CONSTRAINT FK_MatchPlayers_Users FOREIGN KEY (UserId) REFERENCES auth.Users(UserId) ON DELETE NO ACTION
    );
    PRINT N'[OK] Tạo bảng pvp.MatchPlayers.';
END
GO

-- Bảng MatchPlayerAnswers (Chi tiết phản hồi từng câu trong trận đấu)
IF OBJECT_ID(N'pvp.MatchPlayerAnswers', N'U') IS NULL
BEGIN
    CREATE TABLE pvp.MatchPlayerAnswers (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        MatchPlayerId BIGINT NOT NULL,
        QuestionId UNIQUEIDENTIFIER NOT NULL,
        SelectedAnswerIdsJson NVARCHAR(MAX) NULL, -- JSON array các GUID đáp án đã chọn
        TextResponse NVARCHAR(MAX) NULL,          -- Nếu là câu tự luận
        IsCorrect BIT NOT NULL DEFAULT 0,
        ResponseTimeMs INT NOT NULL DEFAULT 0,
        PointsAwarded INT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_MatchPlayerAnswers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_MatchPlayerAnswers_Players FOREIGN KEY (MatchPlayerId) REFERENCES pvp.MatchPlayers(MatchPlayerId) ON DELETE CASCADE,
        CONSTRAINT FK_MatchPlayerAnswers_Questions FOREIGN KEY (QuestionId) REFERENCES quiz.QuizQuestions(QuestionId) ON DELETE NO ACTION
    );
    PRINT N'[OK] Tạo bảng pvp.MatchPlayerAnswers.';
END
GO

-- Bảng Achievements (Hệ thống Thành tựu & Huy hiệu)
IF OBJECT_ID(N'pvp.Achievements', N'U') IS NULL
BEGIN
    CREATE TABLE pvp.Achievements (
        AchievementId INT IDENTITY(1,1) NOT NULL,
        Code NVARCHAR(50) NOT NULL,
        Title NVARCHAR(150) NOT NULL,
        Description NVARCHAR(300) NOT NULL,
        IconUrl NVARCHAR(1000) NULL,
        XpBonus INT NOT NULL DEFAULT 50,
        Category NVARCHAR(50) NOT NULL DEFAULT 'PvP', -- 'PvP' | 'Content' | 'Streak'
        CONSTRAINT PK_Achievements PRIMARY KEY CLUSTERED (AchievementId),
        CONSTRAINT UQ_Achievements_Code UNIQUE (Code)
    );
    PRINT N'[OK] Tạo bảng pvp.Achievements.';
END
GO

-- Bảng UserAchievements (Lưu thành tựu người dùng mở khóa)
IF OBJECT_ID(N'pvp.UserAchievements', N'U') IS NULL
BEGIN
    CREATE TABLE pvp.UserAchievements (
        UserId UNIQUEIDENTIFIER NOT NULL,
        AchievementId INT NOT NULL,
        EarnedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_UserAchievements PRIMARY KEY CLUSTERED (UserId, AchievementId),
        CONSTRAINT FK_UserAchievements_Users FOREIGN KEY (UserId) REFERENCES auth.Users(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_UserAchievements_Achievements FOREIGN KEY (AchievementId) REFERENCES pvp.Achievements(AchievementId) ON DELETE CASCADE
    );
    PRINT N'[OK] Tạo bảng pvp.UserAchievements.';
END
GO

------------------------------------------------------------------------------------------------------
-- 3.5. SCHEMA: [audit] - KIỂM TOÁN HỆ THỐNG & NHẬT KÝ BIẾN ĐỘNG (AUDIT TRAIL)
------------------------------------------------------------------------------------------------------

-- Bảng AdjustmentTransactions (Admin điều chỉnh gián tiếp điểm Elo/XP - Mục 34.4)
IF OBJECT_ID(N'audit.AdjustmentTransactions', N'U') IS NULL
BEGIN
    CREATE TABLE audit.AdjustmentTransactions (
        TransactionId BIGINT IDENTITY(1,1) NOT NULL,
        AdminUserId UNIQUEIDENTIFIER NOT NULL,
        TargetUserId UNIQUEIDENTIFIER NOT NULL,
        FieldAdjusted NVARCHAR(50) NOT NULL, -- 'Rating' | 'XP'
        OldValue INT NOT NULL,
        NewValue INT NOT NULL,
        Delta INT NOT NULL,
        Reason NVARCHAR(500) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_AdjustmentTransactions PRIMARY KEY CLUSTERED (TransactionId),
        CONSTRAINT FK_Adjustment_Admin FOREIGN KEY (AdminUserId) REFERENCES auth.Users(UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_Adjustment_Target FOREIGN KEY (TargetUserId) REFERENCES auth.Users(UserId) ON DELETE NO ACTION
    );
    PRINT N'[OK] Tạo bảng audit.AdjustmentTransactions.';
END
GO

-- Bảng AuditLogs (Nhật ký thay đổi dữ liệu nhạy cảm)
IF OBJECT_ID(N'audit.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE audit.AuditLogs (
        LogId BIGINT IDENTITY(1,1) NOT NULL,
        TableName NVARCHAR(100) NOT NULL,
        ActionType NVARCHAR(20) NOT NULL, -- 'INSERT' | 'UPDATE' | 'DELETE'
        RecordId NVARCHAR(100) NOT NULL,
        ChangedByUserId UNIQUEIDENTIFIER NULL,
        OldDataJson NVARCHAR(MAX) NULL,
        NewDataJson NVARCHAR(MAX) NULL,
        IpAddress NVARCHAR(50) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (LogId)
    );
    PRINT N'[OK] Tạo bảng audit.AuditLogs.';
END
GO

-- ====================================================================================================
-- PHẦN 4: CHIẾN LƯỢC ĐÁNH INDEX TỐI ƯU HIỆU NĂNG (INDEXING STRATEGY)
-- ====================================================================================================

-- 4.1. Index cho bảng auth.Users
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Users_NormalizedEmail' AND object_id = OBJECT_ID(N'auth.Users'))
    CREATE UNIQUE NONCLUSTERED INDEX IX_Users_NormalizedEmail 
    ON auth.Users(NormalizedEmail) 
    WHERE IsDeleted = 0;
GO

-- 4.2. Index cho Bảng Xếp Hạng (Leaderboard UC09) - Covering Index cực nhanh
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Profiles_Rating_Leaderboard' AND object_id = OBJECT_ID(N'auth.Profiles'))
    CREATE NONCLUSTERED INDEX IX_Profiles_Rating_Leaderboard
    ON auth.Profiles(Rating DESC, Wins DESC)
    INCLUDE (UserId, DisplayName, AvatarUrl, RankTier, CurrentXP, Level);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Profiles_XP_Leaderboard' AND object_id = OBJECT_ID(N'auth.Profiles'))
    CREATE NONCLUSTERED INDEX IX_Profiles_XP_Leaderboard
    ON auth.Profiles(CurrentXP DESC)
    INCLUDE (UserId, DisplayName, AvatarUrl, RankTier, Level, Rating);
GO

-- 4.3. Index cho core.Contents - Tối ưu tìm kiếm, lọc danh mục & phân trang Keyset
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Contents_Public_Listing' AND object_id = OBJECT_ID(N'core.Contents'))
    CREATE NONCLUSTERED INDEX IX_Contents_Public_Listing
    ON core.Contents(Visibility, IsDeleted, CreatedAt DESC)
    INCLUDE (ContentId, OwnerId, SubjectId, ContentType, Title, Slug, DifficultyLevel, StarCount, CommentCount, ViewCount)
    WHERE Visibility = 'Public' AND IsDeleted = 0;
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Contents_Subject' AND object_id = OBJECT_ID(N'core.Contents'))
    CREATE NONCLUSTERED INDEX IX_Contents_Subject
    ON core.Contents(SubjectId, Visibility, IsDeleted);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Contents_ForkedFromId' AND object_id = OBJECT_ID(N'core.Contents'))
    CREATE NONCLUSTERED INDEX IX_Contents_ForkedFromId
    ON core.Contents(ForkedFromId)
    WHERE ForkedFromId IS NOT NULL;
GO

-- 4.4. Index cho core.Comments - Hiển thị cây bình luận 2 cấp nhanh
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Comments_Content_Hierarchy' AND object_id = OBJECT_ID(N'core.Comments'))
    CREATE NONCLUSTERED INDEX IX_Comments_Content_Hierarchy
    ON core.Comments(ContentId, ParentCommentId, CreatedAt ASC)
    INCLUDE (UserId, Message, IsHiddenByAdmin);
GO

-- 4.5. Index cho pvp.Matches - Tối ưu tìm trận và thống kê
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Matches_Status_Created' AND object_id = OBJECT_ID(N'pvp.Matches'))
    CREATE NONCLUSTERED INDEX IX_Matches_Status_Created
    ON pvp.Matches(Status, CreatedAt DESC)
    INCLUDE (QuizId, GameMode);
GO

-- 4.6. Index cho pvp.MatchPlayers - Tra cứu lịch sử đấu của User
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MatchPlayers_User_History' AND object_id = OBJECT_ID(N'pvp.MatchPlayers'))
    CREATE NONCLUSTERED INDEX IX_MatchPlayers_User_History
    ON pvp.MatchPlayers(UserId, CreatedAt DESC)
    INCLUDE (MatchId, InitialRating, FinalScore, RatingDelta, FinalRating, IsWinner, XpEarned);
GO

PRINT N'[OK] Đã cấu hình toàn bộ hệ thống Index tối ưu.';
GO

-- ====================================================================================================
-- PHẦN 5: CÁC VIEW BÁO CÁO & TRUY VẤN TỐC ĐỘ CAO (HIGH-PERFORMANCE VIEWS)
-- ====================================================================================================

-- View Bảng xếp hạng Top Đấu Thủ (Elo Rating)
CREATE OR ALTER VIEW core.vw_LeaderboardElo
AS
SELECT 
    p.ProfileId,
    p.UserId,
    p.DisplayName,
    p.AvatarUrl,
    p.Rating,
    p.RankTier,
    p.CurrentXP,
    p.Level,
    p.Wins,
    p.Losses,
    p.CurrentStreak,
    CAST(ROUND((CAST(p.Wins AS FLOAT) / NULLIF(p.Wins + p.Losses, 0)) * 100, 1) AS DECIMAL(5,1)) AS WinRatePercentage,
    DENSE_RANK() OVER (ORDER BY p.Rating DESC, p.Wins DESC) AS GlobalRank
FROM auth.Profiles p
INNER JOIN auth.Users u ON p.UserId = u.UserId
WHERE u.IsActive = 1 AND u.IsDeleted = 0;
GO

-- View Danh sách Đề cương & Quiz công khai kèm thông tin Môn học và Tác giả
CREATE OR ALTER VIEW core.vw_PublicContentsSummary
AS
SELECT 
    c.ContentId,
    c.ContentType,
    c.Title,
    c.Slug,
    c.Description,
    c.DifficultyLevel,
    c.StarCount,
    c.CommentCount,
    c.ViewCount,
    c.ForkCount,
    c.CreatedAt,
    s.SubjectId,
    s.Name AS SubjectName,
    s.Code AS SubjectCode,
    p.UserId AS AuthorUserId,
    p.DisplayName AS AuthorDisplayName,
    p.AvatarUrl AS AuthorAvatarUrl,
    p.RankTier AS AuthorRankTier
FROM core.Contents c
INNER JOIN core.Subjects s ON c.SubjectId = s.SubjectId
INNER JOIN auth.Profiles p ON c.OwnerId = p.UserId
WHERE c.Visibility = 'Public' AND c.IsDeleted = 0;
GO

PRINT N'[OK] Đã tạo các View: core.vw_LeaderboardElo, core.vw_PublicContentsSummary.';
GO

-- ====================================================================================================
-- PHẦN 6: GIAO DỊCH NGUYÊN TỬ (ACID STORED PROCEDURES & BUSINESS LOGIC)
-- ====================================================================================================

------------------------------------------------------------------------------------------------------
-- 6.1. SP Toggle Star (Đánh dấu / Hủy yêu thích nội dung) - Atomic increment / decrement
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE core.sp_ToggleContentStar
    @UserId UNIQUEIDENTIFIER,
    @ContentId UNIQUEIDENTIFIER,
    @IsStarred BIT OUTPUT,
    @NewStarCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON; -- Tự động rollback ngay khi có bất kỳ exception nào

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM core.UserStars WHERE UserId = @UserId AND ContentId = @ContentId)
        BEGIN
            -- Đã Star -> Hủy Star
            DELETE FROM core.UserStars WHERE UserId = @UserId AND ContentId = @ContentId;
            
            UPDATE core.Contents WITH (ROWLOCK)
            SET StarCount = CASE WHEN StarCount > 0 THEN StarCount - 1 ELSE 0 END,
                UpdatedAt = SYSDATETIMEOFFSET()
            WHERE ContentId = @ContentId;

            SET @IsStarred = 0;
        END
        ELSE
        BEGIN
            -- Chưa Star -> Thêm Star
            INSERT INTO core.UserStars (UserId, ContentId, CreatedAt)
            VALUES (@UserId, @ContentId, SYSDATETIMEOFFSET());

            UPDATE core.Contents WITH (ROWLOCK)
            SET StarCount = StarCount + 1,
                UpdatedAt = SYSDATETIMEOFFSET()
            WHERE ContentId = @ContentId;

            SET @IsStarred = 1;
        END

        SELECT @NewStarCount = StarCount FROM core.Contents WHERE ContentId = @ContentId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

------------------------------------------------------------------------------------------------------
-- 6.2. SP Kết thúc Trận đấu PvP & Cập nhật Điểm Elo, XP, Rank (UC08 Real-time Engine)
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE pvp.sp_FinishPvPMatch
    @MatchId UNIQUEIDENTIFIER,
    @WinnerUserId UNIQUEIDENTIFIER,  -- NULL nếu hòa
    @Player1UserId UNIQUEIDENTIFIER,
    @Player1Score INT,
    @Player1TimeMs INT,
    @Player1Delta INT,               -- Delta Elo đã tính bởi thuật toán Elo K=32
    @Player1XpEarned INT,
    @Player2UserId UNIQUEIDENTIFIER,
    @Player2Score INT,
    @Player2TimeMs INT,
    @Player2Delta INT,
    @Player2XpEarned INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Khóa và kiểm tra trạng thái Match để chống xử lý trùng lặp (Double processing)
        DECLARE @CurrentStatus NVARCHAR(50);
        SELECT @CurrentStatus = Status 
        FROM pvp.Matches WITH (UPDLOCK, ROWLOCK)
        WHERE MatchId = @MatchId;

        IF @CurrentStatus IS NULL
        BEGIN
            RAISERROR(N'Trận đấu không tồn tại.', 16, 1);
            RETURN;
        END

        IF @CurrentStatus = 'Finished'
        BEGIN
            -- Đã xử lý rồi, không lặp lại
            COMMIT TRANSACTION;
            RETURN;
        END

        -- 2. Cập nhật trạng thái trận đấu
        UPDATE pvp.Matches
        SET Status = 'Finished',
            FinishedAt = SYSDATETIMEOFFSET()
        WHERE MatchId = @MatchId;

        -- 3. Cập nhật bảng pvp.MatchPlayers cho cả 2 đấu thủ
        UPDATE pvp.MatchPlayers
        SET FinalScore = @Player1Score,
            TotalTimeMs = @Player1TimeMs,
            RatingDelta = @Player1Delta,
            FinalRating = InitialRating + @Player1Delta,
            XpEarned = @Player1XpEarned,
            IsWinner = CASE WHEN @WinnerUserId = @Player1UserId THEN 1 ELSE 0 END
        WHERE MatchId = @MatchId AND UserId = @Player1UserId;

        UPDATE pvp.MatchPlayers
        SET FinalScore = @Player2Score,
            TotalTimeMs = @Player2TimeMs,
            RatingDelta = @Player2Delta,
            FinalRating = InitialRating + @Player2Delta,
            XpEarned = @Player2XpEarned,
            IsWinner = CASE WHEN @WinnerUserId = @Player2UserId THEN 1 ELSE 0 END
        WHERE MatchId = @MatchId AND UserId = @Player2UserId;

        -- 4. Cập nhật Profile Player 1 (Row-lock chặt chẽ)
        UPDATE auth.Profiles WITH (ROWLOCK)
        SET Rating = CASE WHEN (Rating + @Player1Delta) < 0 THEN 0 ELSE (Rating + @Player1Delta) END,
            CurrentXP = CurrentXP + @Player1XpEarned,
            Level = CAST(FLOOR(SQRT((CurrentXP + @Player1XpEarned) / 100.0)) + 1 AS INT),
            Wins = Wins + CASE WHEN @WinnerUserId = @Player1UserId THEN 1 ELSE 0 END,
            Losses = Losses + CASE WHEN @WinnerUserId IS NOT NULL AND @WinnerUserId != @Player1UserId THEN 1 ELSE 0 END,
            CurrentStreak = CASE 
                WHEN @WinnerUserId = @Player1UserId THEN CurrentStreak + 1 
                WHEN @WinnerUserId IS NOT NULL AND @WinnerUserId != @Player1UserId THEN 0 
                ELSE CurrentStreak 
            END,
            BestStreak = CASE 
                WHEN @WinnerUserId = @Player1UserId AND (CurrentStreak + 1) > BestStreak THEN CurrentStreak + 1 
                ELSE BestStreak 
            END,
            RankTier = CASE 
                WHEN (Rating + @Player1Delta) < 1000 THEN N'Tân Binh'
                WHEN (Rating + @Player1Delta) BETWEEN 1000 AND 1499 THEN N'Đồng'
                WHEN (Rating + @Player1Delta) BETWEEN 1500 AND 1999 THEN N'Bạc'
                WHEN (Rating + @Player1Delta) BETWEEN 2000 AND 2499 THEN N'Vàng'
                WHEN (Rating + @Player1Delta) BETWEEN 2500 AND 2999 THEN N'Bạch Kim'
                ELSE N'Kim Cương'
            END,
            UpdatedAt = SYSDATETIMEOFFSET()
        WHERE UserId = @Player1UserId;

        -- 5. Cập nhật Profile Player 2
        UPDATE auth.Profiles WITH (ROWLOCK)
        SET Rating = CASE WHEN (Rating + @Player2Delta) < 0 THEN 0 ELSE (Rating + @Player2Delta) END,
            CurrentXP = CurrentXP + @Player2XpEarned,
            Level = CAST(FLOOR(SQRT((CurrentXP + @Player2XpEarned) / 100.0)) + 1 AS INT),
            Wins = Wins + CASE WHEN @WinnerUserId = @Player2UserId THEN 1 ELSE 0 END,
            Losses = Losses + CASE WHEN @WinnerUserId IS NOT NULL AND @WinnerUserId != @Player2UserId THEN 1 ELSE 0 END,
            CurrentStreak = CASE 
                WHEN @WinnerUserId = @Player2UserId THEN CurrentStreak + 1 
                WHEN @WinnerUserId IS NOT NULL AND @WinnerUserId != @Player2UserId THEN 0 
                ELSE CurrentStreak 
            END,
            BestStreak = CASE 
                WHEN @WinnerUserId = @Player2UserId AND (CurrentStreak + 1) > BestStreak THEN CurrentStreak + 1 
                ELSE BestStreak 
            END,
            RankTier = CASE 
                WHEN (Rating + @Player2Delta) < 1000 THEN N'Tân Binh'
                WHEN (Rating + @Player2Delta) BETWEEN 1000 AND 1499 THEN N'Đồng'
                WHEN (Rating + @Player2Delta) BETWEEN 1500 AND 1999 THEN N'Bạc'
                WHEN (Rating + @Player2Delta) BETWEEN 2000 AND 2499 THEN N'Vàng'
                WHEN (Rating + @Player2Delta) BETWEEN 2500 AND 2999 THEN N'Bạch Kim'
                ELSE N'Kim Cương'
            END,
            UpdatedAt = SYSDATETIMEOFFSET()
        WHERE UserId = @Player2UserId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

------------------------------------------------------------------------------------------------------
-- 6.3. SP Phân trang Keyset Pagination (Thay thế OFFSET/FETCH chậm chạp khi dữ liệu lớn)
------------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE core.sp_GetContentsKeyset
    @SubjectId INT = NULL,
    @ContentType NVARCHAR(50) = NULL,
    @LastCreatedAt DATETIMEOFFSET = NULL,
    @LastContentId UNIQUEIDENTIFIER = NULL,
    @PageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    -- Tối ưu tham số
    IF @PageSize > 100 SET @PageSize = 100;
    IF @PageSize < 1 SET @PageSize = 20;

    SELECT TOP (@PageSize)
        c.ContentId,
        c.ContentType,
        c.Title,
        c.Slug,
        c.DifficultyLevel,
        c.StarCount,
        c.CommentCount,
        c.ViewCount,
        c.ForkCount,
        c.CreatedAt,
        s.Name AS SubjectName,
        s.Code AS SubjectCode,
        p.DisplayName AS AuthorDisplayName,
        p.AvatarUrl AS AuthorAvatarUrl,
        p.RankTier AS AuthorRankTier
    FROM core.Contents c
    INNER JOIN core.Subjects s ON c.SubjectId = s.SubjectId
    INNER JOIN auth.Profiles p ON c.OwnerId = p.UserId
    WHERE c.Visibility = 'Public' 
      AND c.IsDeleted = 0
      AND (@SubjectId IS NULL OR c.SubjectId = @SubjectId)
      AND (@ContentType IS NULL OR c.ContentType = @ContentType)
      AND (
          @LastCreatedAt IS NULL 
          OR (c.CreatedAt < @LastCreatedAt)
          OR (c.CreatedAt = @LastCreatedAt AND c.ContentId < @LastContentId)
      )
    ORDER BY c.CreatedAt DESC, c.ContentId DESC;
END;
GO

PRINT N'[OK] Đã tạo các Stored Procedure cốt lõi.';
GO

-- ====================================================================================================
-- PHẦN 7: BẢO MẬT & PHÂN QUYỀN ĐẶC QUYỀN TỐI THIỂU (PRINCIPLE OF LEAST PRIVILEGE)
-- ====================================================================================================

-- Tạo Database Role riêng cho Application (Web API .NET)
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'db_educlash_app_role' AND type = 'R')
BEGIN
    CREATE ROLE db_educlash_app_role;
    PRINT N'[OK] Tạo Database Role: db_educlash_app_role.';
END
GO

-- Cấp quyền DML hạn chế theo schema, KHÔNG CẤP QUYỀN DDL (DROP/ALTER)
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::auth TO db_educlash_app_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::core TO db_educlash_app_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::quiz TO db_educlash_app_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::pvp TO db_educlash_app_role;
GRANT SELECT, INSERT ON SCHEMA::audit TO db_educlash_app_role; -- Chỉ cho phép ghi & xem audit, không cho sửa/xóa audit
GRANT EXECUTE ON SCHEMA::core TO db_educlash_app_role;
GRANT EXECUTE ON SCHEMA::pvp TO db_educlash_app_role;

-- Cấm hoàn toàn hành động phá hoại đối với App Role
DENY ALTER TO db_educlash_app_role;
DENY DELETE ON audit.AuditLogs TO db_educlash_app_role;
DENY DELETE ON audit.AdjustmentTransactions TO db_educlash_app_role;

PRINT N'[OK] Đã cấu hình phân quyền đặc quyền tối thiểu (Least Privilege).';
GO

-- ====================================================================================================
-- PHẦN 8: BẢO TRÌ, CHỐNG PHÂN MẢNH INDEX & GIÁM SÁT DỰ PHÒNG (MONITORING & DR)
-- ====================================================================================================

-- 8.1. SP Bảo trì Index tự động (Reorganize khi 10-30%, Rebuild khi > 30%)
CREATE OR ALTER PROCEDURE dbo.sp_MaintainDatabaseIndexes
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TableName NVARCHAR(256);
    DECLARE @IndexName NVARCHAR(256);
    DECLARE @FragPercent FLOAT;
    DECLARE @SQL NVARCHAR(MAX);

    DECLARE curIndexes CURSOR LOCAL FAST_FORWARD FOR
        SELECT 
            dbschemas.name + '.' + dbtables.name AS TableName,
            dbindexes.name AS IndexName,
            indexstats.avg_fragmentation_in_percent AS FragPercent
        FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') indexstats
        INNER JOIN sys.tables dbtables ON dbtables.object_id = indexstats.object_id
        INNER JOIN sys.schemas dbschemas ON dbtables.schema_id = dbschemas.schema_id
        INNER JOIN sys.indexes dbindexes ON dbindexes.object_id = indexstats.object_id
            AND indexstats.index_id = dbindexes.index_id
        WHERE indexstats.avg_fragmentation_in_percent >= 10.0
          AND indexstats.page_count > 64 -- Chỉ xét các index có ít nhất 64 trang (512 KB)
          AND dbindexes.name IS NOT NULL;

    OPEN curIndexes;
    FETCH NEXT FROM curIndexes INTO @TableName, @IndexName, @FragPercent;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @FragPercent >= 30.0
        BEGIN
            SET @SQL = N'ALTER INDEX [' + @IndexName + N'] ON ' + @TableName + N' REBUILD WITH (ONLINE = ON);';
            BEGIN TRY
                EXEC sp_executesql @SQL;
                PRINT N'Rebuilt Index: ' + @IndexName + N' on ' + @TableName;
            END TRY
            BEGIN CATCH
                -- Fallback offline nếu phiên bản không hỗ trợ online rebuild
                SET @SQL = N'ALTER INDEX [' + @IndexName + N'] ON ' + @TableName + N' REBUILD;';
                EXEC sp_executesql @SQL;
            END CATCH
        END
        ELSE
        BEGIN
            SET @SQL = N'ALTER INDEX [' + @IndexName + N'] ON ' + @TableName + N' REORGANIZE;';
            EXEC sp_executesql @SQL;
            PRINT N'Reorganized Index: ' + @IndexName + N' on ' + @TableName;
        END

        FETCH NEXT FROM curIndexes INTO @TableName, @IndexName, @FragPercent;
    END

    CLOSE curIndexes;
    DEALLOCATE curIndexes;

    -- Cập nhật Statistics
    EXEC sp_updatestats;
    PRINT N'[OK] Đã hoàn tất bảo trì Index và làm mới số liệu thống kê (Statistics).';
END;
GO

-- 8.2. Mẫu script Backup định kỳ (Database Recovery)
/*
-- 1. Full Backup (Hàng ngày lúc 01:00 AM)
BACKUP DATABASE EduClashDB 
TO DISK = N'C:\Backups\EduClashDB_Full.bak' 
WITH FORMAT, INIT, COMPRESSION, CHECKSUM;

-- 2. Differential Backup (Mỗi 6 tiếng)
BACKUP DATABASE EduClashDB 
TO DISK = N'C:\Backups\EduClashDB_Diff.bak' 
WITH DIFFERENTIAL, COMPRESSION, CHECKSUM;

-- 3. Transaction Log Backup (Mỗi 15 phút - RPO 15 phút)
BACKUP LOG EduClashDB 
TO DISK = N'C:\Backups\EduClashDB_Log.trn' 
WITH COMPRESSION, CHECKSUM;
*/

-- ====================================================================================================
-- PHẦN 9: DỮ LIỆU KHỞI TẠO MẪU (SEED DATA - IDEMPOTENT)
-- ====================================================================================================

-- 9.1. Seed Roles
SET IDENTITY_INSERT auth.Roles ON;
IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE RoleId = 1)
    INSERT INTO auth.Roles (RoleId, RoleName, Description) VALUES (1, 'Guest', N'Khách vãng lai xem công khai');
IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE RoleId = 2)
    INSERT INTO auth.Roles (RoleId, RoleName, Description) VALUES (2, 'User', N'Người dùng đã xác thực');
IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE RoleId = 3)
    INSERT INTO auth.Roles (RoleId, RoleName, Description) VALUES (3, 'Creator', N'Tác giả biên soạn Đề cương & Quiz');
IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE RoleId = 4)
    INSERT INTO auth.Roles (RoleId, RoleName, Description) VALUES (4, 'Player', N'Đấu thủ tham gia luyện tập & PvP');
IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE RoleId = 5)
    INSERT INTO auth.Roles (RoleId, RoleName, Description) VALUES (5, 'Admin', N'Quản trị viên toàn hệ thống');
SET IDENTITY_INSERT auth.Roles OFF;
GO

-- 9.2. Seed Subjects
IF NOT EXISTS (SELECT 1 FROM core.Subjects WHERE NormalizedCode = 'CS101')
    INSERT INTO core.Subjects (Code, NormalizedCode, Name, Description, IsOfficial)
    VALUES ('CS101', 'CS101', N'Nhập môn Lập trình', N'Khái niệm biến, kiểu dữ liệu, vòng lặp và hàm cơ bản', 1);

IF NOT EXISTS (SELECT 1 FROM core.Subjects WHERE NormalizedCode = 'DB201')
    INSERT INTO core.Subjects (Code, NormalizedCode, Name, Description, IsOfficial)
    VALUES ('DB201', 'DB201', N'Hệ Quản trị Cơ sở Dữ liệu', N'Mô hình quan hệ, chuẩn hóa 3NF, SQL và chuẩn ACID', 1);

IF NOT EXISTS (SELECT 1 FROM core.Subjects WHERE NormalizedCode = 'SE301')
    INSERT INTO core.Subjects (Code, NormalizedCode, Name, Description, IsOfficial)
    VALUES ('SE301', 'SE301', N'Phân tích Thiết kế Hệ thống', N'UML, Use Case, ERD, RESTful API và Kiến trúc phần mềm', 1);
GO

-- 9.3. Seed Tags mẫu
IF NOT EXISTS (SELECT 1 FROM core.Tags WHERE NormalizedTagName = 'HUIT')
    INSERT INTO core.Tags (TagName, NormalizedTagName, UsageCount) VALUES ('#HUIT', 'HUIT', 10);
IF NOT EXISTS (SELECT 1 FROM core.Tags WHERE NormalizedTagName = 'SQL')
    INSERT INTO core.Tags (TagName, NormalizedTagName, UsageCount) VALUES ('#SQL', 'SQL', 25);
IF NOT EXISTS (SELECT 1 FROM core.Tags WHERE NormalizedTagName = 'OOP')
    INSERT INTO core.Tags (TagName, NormalizedTagName, UsageCount) VALUES ('#OOP', 'OOP', 18);
GO

-- 9.4. Seed Thành tựu (Achievements)
IF NOT EXISTS (SELECT 1 FROM pvp.Achievements WHERE Code = 'FIRST_BLOOD')
    INSERT INTO pvp.Achievements (Code, Title, Description, XpBonus, Category)
    VALUES ('FIRST_BLOOD', N'Chiến Công Đầu', N'Thắng trận đấu PvP đầu tiên', 100, 'PvP');

IF NOT EXISTS (SELECT 1 FROM pvp.Achievements WHERE Code = 'STREAK_5')
    INSERT INTO pvp.Achievements (Code, Title, Description, XpBonus, Category)
    VALUES ('STREAK_5', N'Bất Khả Chiến Bại', N'Đạt chuỗi thắng 5 trận liên tiếp', 250, 'Streak');

IF NOT EXISTS (SELECT 1 FROM pvp.Achievements WHERE Code = 'TOP_CREATOR')
    INSERT INTO pvp.Achievements (Code, Title, Description, XpBonus, Category)
    VALUES ('TOP_CREATOR', N'Tác Giả Vàng', N'Đề cương đạt mốc 50 lượt Star', 300, 'Content');
GO

PRINT N'====================================================================================================';
PRINT N'[THÀNH CÔNG] TOÀN BỘ CƠ SỞ DỮ LIỆU EDUCLASH CHUẨN ACID, 3NF & HIỆU NĂNG ĐÃ SẴN SÀNG TRIỂN KHAI!';
PRINT N'====================================================================================================';
