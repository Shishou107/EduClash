using System;
using System.Collections.Generic;

namespace api_Outline_management.DTOs;

public class ParsedQuestionDto
{
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "SingleChoice";
    public int Points { get; set; } = 10;
    public string? Explanation { get; set; }
    public List<ParsedAnswerDto> Answers { get; set; } = new();
}

public class ParsedAnswerDto
{
    public string AnswerText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; }
}

public class OutlineSectionDto
{
    public string SectionTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}

public class UploadOutlineRequestDto
{
    public string Title { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = "Medium";
    public string Mode { get; set; } = "TheoryOutline"; // 'TheoryOutline' | 'StructuredQuiz'
    public List<string> Tags { get; set; } = new();
}
