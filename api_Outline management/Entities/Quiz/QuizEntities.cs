using System;
using System.Collections.Generic;
using api_Outline_management.Entities.Core;
using api_Outline_management.Entities.PvP;

namespace api_Outline_management.Entities.Quiz;

public class StudyOutline
{
    public Guid ContentId { get; set; }
    public string RichTextContent { get; set; } = string.Empty;
    public string? AttachedFileUrl { get; set; }
    public long? AttachedFileSizeBytes { get; set; }
    public string? TableOfContentsJson { get; set; }

    public virtual Content Content { get; set; } = null!;
}

public class Quiz
{
    public Guid ContentId { get; set; }
    public int TotalQuestions { get; set; }
    public int TimePerQuestionSec { get; set; } = 30;
    public int PassThresholdPercentage { get; set; } = 70;
    public bool AllowShuffleQuestions { get; set; } = true;
    public bool AllowShuffleAnswers { get; set; } = true;
    public bool IsPvPEnabled { get; set; } = true;

    public virtual Content Content { get; set; } = null!;
    public virtual ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();
}

public class QuizQuestion
{
    public Guid QuestionId { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "SingleChoice"; // 'SingleChoice' | 'MultipleChoice' | 'Essay' | 'TrueFalse'
    public int Points { get; set; } = 10;
    public int OrderIndex { get; set; } = 1;
    public string? Explanation { get; set; }
    public string? MediaUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual Quiz Quiz { get; set; } = null!;
    public virtual ICollection<QuestionAnswer> Answers { get; set; } = new List<QuestionAnswer>();
}

public class QuestionAnswer
{
    public Guid AnswerId { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; } = 1;

    public virtual QuizQuestion Question { get; set; } = null!;
}
