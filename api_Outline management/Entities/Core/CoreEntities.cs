using System;
using System.Collections.Generic;
using api_Outline_management.Entities.Auth;
using api_Outline_management.Entities.Quiz;

namespace api_Outline_management.Entities.Core;

public class Subject
{
    public int SubjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsOfficial { get; set; } = true;
    public Guid? ProposedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual User? ProposedByUser { get; set; }
    public virtual ICollection<Content> Contents { get; set; } = new List<Content>();
}

public class Tag
{
    public int TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string NormalizedTagName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual ICollection<ContentTag> ContentTags { get; set; } = new List<ContentTag>();
}

public class Content
{
    public Guid ContentId { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public int SubjectId { get; set; }
    public string ContentType { get; set; } = "StudyOutline"; // 'StudyOutline' | 'Quiz'
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Visibility { get; set; } = "Public"; // 'Public' | 'Private' | 'Unlisted'
    public string DifficultyLevel { get; set; } = "Medium"; // 'Easy' | 'Medium' | 'Hard'
    public Guid? ForkedFromId { get; set; }

    // Controlled denormalized counters
    public int StarCount { get; set; }
    public int CommentCount { get; set; }
    public int ViewCount { get; set; }
    public int ForkCount { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = null!;

    // Navigations
    public virtual User Owner { get; set; } = null!;
    public virtual Subject Subject { get; set; } = null!;
    public virtual Content? ForkedFrom { get; set; }
    public virtual ICollection<Content> ForkedCopies { get; set; } = new List<Content>();
    public virtual ICollection<ContentTag> ContentTags { get; set; } = new List<ContentTag>();
    public virtual ICollection<UserStar> UserStars { get; set; } = new List<UserStar>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    // 1:1 Specializations (TPT)
    public virtual StudyOutline? StudyOutline { get; set; }
    public virtual Entities.Quiz.Quiz? Quiz { get; set; }
}

public class ContentTag
{
    public Guid ContentId { get; set; }
    public int TagId { get; set; }

    public virtual Content Content { get; set; } = null!;
    public virtual Tag Tag { get; set; } = null!;
}

public class UserStar
{
    public Guid UserId { get; set; }
    public Guid ContentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual User User { get; set; } = null!;
    public virtual Content Content { get; set; } = null!;
}

public class Comment
{
    public long CommentId { get; set; }
    public Guid ContentId { get; set; }
    public Guid UserId { get; set; }
    public long? ParentCommentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsHiddenByAdmin { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public virtual Content Content { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual Comment? ParentComment { get; set; }
    public virtual ICollection<Comment> Replies { get; set; } = new List<Comment>();
}

public class Report
{
    public long ReportId { get; set; }
    public Guid ReporterUserId { get; set; }
    public Guid? TargetContentId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // 'Pending' | 'Resolved' | 'Dismissed'
    public string? AdminNote { get; set; }
    public Guid? ResolvedByAdminId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual User ReporterUser { get; set; } = null!;
    public virtual Content? TargetContent { get; set; }
    public virtual User? TargetUser { get; set; }
    public virtual User? ResolvedByAdmin { get; set; }
}
