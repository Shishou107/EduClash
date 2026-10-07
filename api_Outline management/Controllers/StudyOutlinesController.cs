using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api_Outline_management.Data;
using api_Outline_management.DTOs;
using api_Outline_management.Entities.Core;
using api_Outline_management.Entities.Quiz;
using api_Outline_management.Services;

namespace api_Outline_management.Controllers;

public class UploadOutlineFormDto
{
    public required IFormFile File { get; set; }
    public required string Title { get; set; }
    public int SubjectId { get; set; }
    public string? Description { get; set; }
    public string DifficultyLevel { get; set; } = "Medium";
    public string Mode { get; set; } = "TheoryOutline"; // 'TheoryOutline' | 'StructuredQuiz'
    public string? Tags { get; set; }
}

[ApiController]
[Route("api/v1/study-outlines")]
public class StudyOutlinesController : ControllerBase
{
    private readonly EduClashDbContext _context;
    private readonly IFileParserService _fileParser;
    private readonly IAiQuizGeneratorService _aiQuizGenerator;
    private readonly ICoinService _coinService;
    private readonly IWebHostEnvironment _env;

    public StudyOutlinesController(
        EduClashDbContext context,
        IFileParserService fileParser,
        IAiQuizGeneratorService aiQuizGenerator,
        ICoinService coinService,
        IWebHostEnvironment env)
    {
        _context = context;
        _fileParser = fileParser;
        _aiQuizGenerator = aiQuizGenerator;
        _coinService = coinService;
        _env = env;
    }

    /// <summary>
    /// Import file DOCX / PDF đề cương lý thuyết hoặc bộ đề trắc nghiệm có sẵn
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadOutline([FromForm] UploadOutlineFormDto request)
    {
        var file = request.File;
        var title = request.Title;
        var subjectId = request.SubjectId;
        var description = request.Description;
        var difficultyLevel = request.DifficultyLevel;
        var mode = request.Mode;
        var tags = request.Tags;

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { Message = "Vui lòng đính kèm file DOCX hoặc PDF." });
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".docx" && ext != ".pdf")
        {
            return BadRequest(new { Message = "Hệ thống chỉ hỗ trợ định dạng .docx và .pdf." });
        }

        // 1. Lưu file vật lý vào thư mục wwwroot/uploads/outlines/
        var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "outlines");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var savedFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, savedFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 2. Trích xuất text từ file thông qua IFileParserService (đọc từ file vừa lưu trên đĩa để đảm bảo toàn vẹn stream)
        string extractedText;
        using (var readStream = System.IO.File.OpenRead(filePath))
        {
            extractedText = await _fileParser.ExtractTextAsync(readStream, file.FileName);
        }

        // 3. Lấy hoặc gán Owner (mặc định lấy user đầu tiên nếu không có token)
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid ownerId;
        if (!Guid.TryParse(userIdStr, out ownerId))
        {
            var firstUser = await _context.Users.FirstOrDefaultAsync();
            ownerId = firstUser?.UserId ?? Guid.NewGuid();
        }

        var contentId = Guid.NewGuid();
        var slug = GenerateSlug(title);

        var content = new Content
        {
            ContentId = contentId,
            OwnerId = ownerId,
            SubjectId = subjectId,
            ContentType = mode == "StructuredQuiz" ? "Quiz" : "StudyOutline",
            Title = title,
            Slug = slug,
            Description = description,
            DifficultyLevel = difficultyLevel,
            Visibility = "Public",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Contents.Add(content);

        // 4. Xử lý Tags (Tự động chuẩn hóa bỏ dấu/khoảng trắng thành dạng #tag)
        if (!string.IsNullOrWhiteSpace(tags))
        {
            var rawList = (tags.Contains(',') || tags.Contains(';'))
                ? tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                : tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var processedTags = rawList
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Select(t =>
                {
                    var clean = TagHelper.RemoveDiacriticsAndSpaces(t);
                    var tagName = t.StartsWith("#") ? t : "#" + (string.IsNullOrEmpty(clean) ? t : clean);
                    var normUpper = (string.IsNullOrEmpty(clean) ? TagHelper.RemoveDiacriticsAndSpaces(t) : clean).ToUpperInvariant();
                    return new { TagName = tagName, NormalizedTagName = normUpper };
                })
                .Where(x => !string.IsNullOrEmpty(x.NormalizedTagName))
                .GroupBy(x => x.NormalizedTagName)
                .Select(g => g.First())
                .ToList();

            foreach (var item in processedTags)
            {
                var tagEntity = await _context.Tags.FirstOrDefaultAsync(t => t.NormalizedTagName == item.NormalizedTagName);
                if (tagEntity == null)
                {
                    tagEntity = new Tag { TagName = item.TagName, NormalizedTagName = item.NormalizedTagName, UsageCount = 1 };
                    _context.Tags.Add(tagEntity);
                }
                else
                {
                    tagEntity.UsageCount++;
                }

                _context.ContentTags.Add(new ContentTag { ContentId = contentId, Tag = tagEntity });
            }
        }

        // 5. Phân nhánh theo Mode
        if (mode == "StructuredQuiz")
        {
            Console.WriteLine($"[PARSE QUIZ] ExtractedText Length: {extractedText?.Length ?? 0}");
            if (!string.IsNullOrEmpty(extractedText))
            {
                var preview = extractedText.Length > 400 ? extractedText.Substring(0, 400) : extractedText;
                Console.WriteLine($"[PARSE QUIZ] ExtractedText Preview:\n{preview}\n-----------------");
            }

            // Bóc tách đề trắc nghiệm có sẵn: Câu 1 ... A B C D
            var parsedQuestions = _fileParser.ParseStructuredQuiz(extractedText ?? "");
            Console.WriteLine($"[PARSE QUIZ] Parsed Questions Count: {parsedQuestions?.Count ?? 0}");

            if (parsedQuestions == null || parsedQuestions.Count == 0)
            {
                if (System.IO.File.Exists(filePath))
                {
                    try { System.IO.File.Delete(filePath); } catch { }
                }
                return BadRequest(new { Message = "Tài liệu tải lên không đúng định dạng đề cương trắc nghiệm (cần có các câu hỏi và các phương án lựa chọn A, B, C, D...). Vui lòng kiểm tra lại file hoặc chọn đúng loại tài liệu (Đề cương lý thuyết)!" });
            }

            var quiz = new Quiz
            {
                ContentId = contentId,
                TotalQuestions = parsedQuestions.Count,
                TimePerQuestionSec = 30,
                PassThresholdPercentage = 70,
                IsPvPEnabled = true
            };

            int order = 1;
            foreach (var pq in parsedQuestions)
            {
                var qEntity = new QuizQuestion
                {
                    QuestionId = Guid.NewGuid(),
                    QuizId = contentId,
                    QuestionText = pq.QuestionText,
                    QuestionType = pq.QuestionType,
                    Points = pq.Points,
                    OrderIndex = order++
                };

                int ansOrder = 1;
                foreach (var pa in pq.Answers)
                {
                    qEntity.Answers.Add(new QuestionAnswer
                    {
                        AnswerId = Guid.NewGuid(),
                        QuestionId = qEntity.QuestionId,
                        AnswerText = pa.AnswerText,
                        IsCorrect = pa.IsCorrect,
                        OrderIndex = ansOrder++
                    });
                }
                quiz.Questions.Add(qEntity);
            }

            _context.Quizzes.Add(quiz);
        }
        else
        {
            // Đề cương lý thuyết: Lưu nguyên bản tập tin DOCX / PDF theo chuẩn Studocu / Word Online
            // Không bóc tách thành các mục sổ xuống (Collapsible Sections)
            var outline = new StudyOutline
            {
                ContentId = contentId,
                RichTextContent = extractedText ?? string.Empty,
                AttachedFileUrl = $"/uploads/outlines/{savedFileName}",
                AttachedFileSizeBytes = file.Length,
                TableOfContentsJson = null
            };

            _context.StudyOutlines.Add(outline);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            ContentId = contentId,
            Title = title,
            Mode = mode,
            FileUrl = $"/uploads/outlines/{savedFileName}",
            Message = mode == "StructuredQuiz" 
                ? "Đã import và bóc tách thành công bộ câu hỏi trắc nghiệm!" 
                : "Đã lưu nguyên bản đề cương lý thuyết (PDF/Word) chuẩn Studocu thành công!"
        });
    }

    /// <summary>
    /// Sử dụng Xu (EduCoin) để nhờ AI sinh bộ trắc nghiệm từ Đề cương lý thuyết
    /// </summary>
    [HttpPost("{id:guid}/generate-ai-quiz")]
    public async Task<IActionResult> GenerateAiQuiz(Guid id, [FromQuery] int coins = 3, [FromQuery] int? questionCount = null)
    {
        var outline = await _context.StudyOutlines
            .Include(o => o.Content)
            .FirstOrDefaultAsync(o => o.ContentId == id);

        if (outline == null)
        {
            return NotFound(new { Message = "Không tìm thấy đề cương lý thuyết." });
        }

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid userId;
        if (!Guid.TryParse(userIdStr, out userId))
        {
            var firstUser = await _context.Users.FirstOrDefaultAsync();
            userId = firstUser?.UserId ?? outline.Content.OwnerId;
        }

        var coinCost = Math.Max(1, coins);
        var totalQuestions = questionCount.HasValue && questionCount.Value > 0 ? questionCount.Value : coinCost * 2;

        // Gọi AI Service (khấu trừ coinCost Xu theo quy chuẩn 1 xu = 2 câu)
        var (success, message, questions) = await _aiQuizGenerator.GenerateQuizFromTheoryAsync(
            userId, 
            outline.RichTextContent, 
            coinCost,
            totalQuestions);

        if (!success)
        {
            return BadRequest(new { Message = message });
        }

        // Lưu Quiz mới được sinh bởi AI
        var quizContentId = Guid.NewGuid();
        var quizContent = new Content
        {
            ContentId = quizContentId,
            OwnerId = userId,
            SubjectId = outline.Content.SubjectId,
            ContentType = "Quiz",
            Title = $"[AI Quiz] {outline.Content.Title}",
            Slug = GenerateSlug($"ai-quiz-{outline.Content.Title}"),
            Description = $"Bộ câu hỏi trắc nghiệm được AI sinh tự động từ đề cương '{outline.Content.Title}'",
            DifficultyLevel = outline.Content.DifficultyLevel,
            ForkedFromId = outline.ContentId,
            Visibility = "Public"
        };

        var quiz = new Quiz
        {
            ContentId = quizContentId,
            TotalQuestions = questions.Count,
            TimePerQuestionSec = 30,
            PassThresholdPercentage = 70,
            IsPvPEnabled = true
        };

        int qOrder = 1;
        foreach (var q in questions)
        {
            var qEntity = new QuizQuestion
            {
                QuestionId = Guid.NewGuid(),
                QuizId = quizContentId,
                QuestionText = q.QuestionText,
                Explanation = q.Explanation,
                Points = q.Points,
                OrderIndex = qOrder++
            };

            int aOrder = 1;
            foreach (var a in q.Answers)
            {
                qEntity.Answers.Add(new QuestionAnswer
                {
                    AnswerId = Guid.NewGuid(),
                    QuestionId = qEntity.QuestionId,
                    AnswerText = a.AnswerText,
                    IsCorrect = a.IsCorrect,
                    OrderIndex = aOrder++
                });
            }
            quiz.Questions.Add(qEntity);
        }

        _context.Contents.Add(quizContent);
        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync();

        var balance = await _coinService.GetBalanceAsync(userId);

        return Ok(new
        {
            QuizId = quizContentId,
            QuizTitle = quizContent.Title,
            QuestionCount = questions.Count,
            RemainingCoins = balance,
            Message = message
        });
    }

    /// <summary>
    /// Lấy chi tiết đề cương lý thuyết kèm danh sách các mục sổ xuống (Collapsible Sections)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOutlineDetail(Guid id)
    {
        var outline = await _context.StudyOutlines
            .AsNoTracking()
            .Include(o => o.Content)
            .ThenInclude(c => c.Subject)
            .Include(o => o.Content.Owner)
            .ThenInclude(u => u.Profile)
            .Include(o => o.Content.ContentTags)
            .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(o => o.ContentId == id && !o.Content.IsDeleted);

        if (outline == null)
        {
            return NotFound(new { Message = "Đề cương không tồn tại." });
        }

        List<OutlineSectionDto> sections = new();
        if (!string.IsNullOrEmpty(outline.TableOfContentsJson))
        {
            try
            {
                sections = JsonSerializer.Deserialize<List<OutlineSectionDto>>(outline.TableOfContentsJson) ?? new();
            }
            catch { /* Ignore deserialization error */ }
        }

        return Ok(new
        {
            outline.ContentId,
            OwnerId = outline.Content.OwnerId,
            outline.Content.Title,
            outline.Content.Description,
            outline.Content.DifficultyLevel,
            outline.Content.StarCount,
            outline.Content.ViewCount,
            outline.Content.CreatedAt,
            outline.AttachedFileUrl,
            outline.AttachedFileSizeBytes,
            Subject = new { outline.Content.Subject.SubjectId, outline.Content.Subject.Code, outline.Content.Subject.Name },
            Author = new { UserId = outline.Content.Owner.UserId, outline.Content.Owner.Profile!.DisplayName, outline.Content.Owner.Profile.AvatarUrl, outline.Content.Owner.Profile.RankTier },
            Tags = outline.Content.ContentTags.Select(ct => ct.Tag.TagName).ToList(),
            Sections = sections,
            RawContent = outline.RichTextContent
        });
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant().Trim();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
        return $"{slug}-{DateTime.UtcNow.Ticks % 10000}";
    }
}
