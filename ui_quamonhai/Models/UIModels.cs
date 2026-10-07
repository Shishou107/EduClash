using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ui_quamonhai.Models;

public class ContentItemViewModel
{
    public Guid ContentId { get; set; } = Guid.NewGuid();
    public string ContentType { get; set; } = "StudyOutline"; // 'StudyOutline' | 'Quiz'
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = "Medium"; // 'Easy' | 'Medium' | 'Hard'
    public int StarCount { get; set; }
    public int CommentCount { get; set; }
    public int ViewCount { get; set; }
    public int ForkCount { get; set; }
    public int QuestionCount { get; set; } = 20; // Dùng cho Quiz
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsStarredByMe { get; set; }

    public SubjectBriefViewModel Subject { get; set; } = new();
    public AuthorBriefViewModel Author { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class SubjectBriefViewModel
{
    public int SubjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class AuthorBriefViewModel
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string RankTier { get; set; } = "Đồng";
}

public class SubjectViewModel
{
    public int SubjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string ThemeColor { get; set; } = "#7F1D1D";
    public string TextColor { get; set; } = "#FFFFFF";
    public bool IsOfficial { get; set; } = true;
    public int ContentsCount { get; set; }
}

public class LeaderboardItemViewModel
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int Rating { get; set; }
    public string RankTier { get; set; } = "Đồng";
    public int Level { get; set; } = 1;
    public long CurrentXP { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int CurrentStreak { get; set; }
    public double WinRate { get; set; }
    public int RankPosition { get; set; }
}

public class QuestionViewModel
{
    public Guid QuestionId { get; set; } = Guid.NewGuid();
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "SingleChoice";
    public List<AnswerViewModel> Answers { get; set; } = new();
    public int Points { get; set; } = 10;
    public Guid? SelectedAnswerId { get; set; }
    public HashSet<Guid> SelectedAnswerIds { get; set; } = new();
    public string? Explanation { get; set; }

    public bool IsMultipleChoice => QuestionType == "MultipleChoice" || Answers.Count(a => a.IsCorrect) > 1;
}

public class AnswerViewModel
{
    public Guid AnswerId { get; set; } = Guid.NewGuid();
    public string AnswerText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}

public class UserProfileViewModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string Role { get; set; } = "Member"; // "Guest" | "Member" | "Admin"
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
    public int Rating { get; set; } = 1200;
    public string RankTier { get; set; } = "Đồng";
    public int Level { get; set; } = 1;
    public long CurrentXP { get; set; } = 0;
    public int Coins { get; set; } = 50; // Xu sử dụng AI
    public int Wins { get; set; } = 0;
    public int Losses { get; set; } = 0;
    public int CurrentStreak { get; set; } = 0;
    public int BestStreak { get; set; } = 0;
}

public class OutlineDetailViewModel
{
    public Guid ContentId { get; set; }
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = "Medium";
    public int StarCount { get; set; }
    public int ViewCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? AttachedFileUrl { get; set; }
    public long? AttachedFileSizeBytes { get; set; }
    public SubjectBriefViewModel Subject { get; set; } = new();
    public AuthorBriefViewModel Author { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<OutlineSectionItemViewModel> Sections { get; set; } = new();
    public string RawContent { get; set; } = string.Empty;
}

public class OutlineSectionItemViewModel
{
    public string SectionTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public bool IsExpanded { get; set; } = false;
}

public class UploadOutlineFormModel
{
    public string Title { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = "Medium";
    public string Mode { get; set; } = "TheoryOutline"; // 'TheoryOutline' | 'StructuredQuiz'
}

// Authentication Models
public class RegisterRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 60 ký tự")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ email")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [MinLength(8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Mật khẩu phải chứa ít nhất 1 chữ hoa và 1 chữ số")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [RegularExpression(@"^$|^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại Việt Nam không hợp lệ (10 số, bắt đầu bằng 03, 05, 07, 08, 09)")]
    public string? PhoneNumber { get; set; }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ email")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = true;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public UserProfileViewModel User { get; set; } = new();
}

public class UpdateProfileRequest
{
    [Required(ErrorMessage = "Họ tên không được để trống")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 60 ký tự")]
    public string DisplayName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    [RegularExpression(@"^$|^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại Việt Nam không hợp lệ")]
    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới tối thiểu 8 ký tự")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Mật khẩu phải chứa ít nhất 1 chữ hoa và 1 chữ số")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

// Subscription & Coin Models
public class SubscriptionPlan
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PriceVnd { get; set; }
    public int Coins { get; set; }
    public bool IsRecommended { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class PurchaseRequest
{
    public string PlanId { get; set; } = string.Empty;
}

public class QuizDetailViewModel
{
    public Guid QuizId { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SubjectBriefViewModel Subject { get; set; } = new();
    public int TimeLimitSeconds { get; set; } = 900; // 15 phút
    public List<QuestionViewModel> Questions { get; set; } = new();
}

public class ArenaPlayerDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Đấu thủ";
    public string? AvatarUrl { get; set; }
    public int Rating { get; set; } = 1000;
    public int Level { get; set; } = 1;
    public string RankTier { get; set; } = "Đồng";
    public int Hp { get; set; } = 200;
    public int Score { get; set; } = 0;
    public int CorrectCount { get; set; } = 0;
    public int CurrentQuestionIndex { get; set; } = 0;
    public bool HasAnsweredCurrent { get; set; } = false;
    public bool LastAnswerWasCorrect { get; set; } = false;
    public DateTime? LastActionTime { get; set; }
    public bool IsFinished { get; set; } = false;
}

public class ArenaRoomDto
{
    public string RoomCode { get; set; } = string.Empty;
    public Guid ContentId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public ArenaPlayerDto Host { get; set; } = new();
    public ArenaPlayerDto? Guest { get; set; }
    public string Status { get; set; } = "Waiting"; // "Waiting" | "Playing" | "Finished"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? WinnerPlayerId { get; set; }
    public string? AbandonedByPlayerId { get; set; }
    public string? WinReason { get; set; }
    public int TotalQuestions { get; set; } = 20;
    public int Seed { get; set; } = 42;
}

public class CreateRoomRequest
{
    public Guid ContentId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Đấu thủ";
    public string? AvatarUrl { get; set; }
    public int Rating { get; set; } = 1000;
    public int Level { get; set; } = 1;
    public string RankTier { get; set; } = "Đồng";
}

public class JoinRoomRequest
{
    public string RoomCode { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Đấu thủ";
    public string? AvatarUrl { get; set; }
    public int Rating { get; set; } = 1000;
    public int Level { get; set; } = 1;
    public string RankTier { get; set; } = "Đồng";
}

public class SubmitAnswerRequest
{
    public string RoomCode { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public int QuestionIndex { get; set; }
    public bool IsCorrect { get; set; }
    public bool IsFinished { get; set; } = false;
}

