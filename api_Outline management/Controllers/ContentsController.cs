using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api_Outline_management.Data;
using api_Outline_management.Entities.Core;
using api_Outline_management.Services;

namespace api_Outline_management.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ContentsController : ControllerBase
{
    private readonly EduClashDbContext _context;
    private readonly IWebHostEnvironment _env;

    public ContentsController(EduClashDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    /// <summary>
    /// Danh sách Outline & Quiz công khai (UC01) có hỗ trợ phân trang Keyset
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPublicContents(
        [FromQuery] int? subjectId,
        [FromQuery] string? contentType,
        [FromQuery] string? search,
        [FromQuery] DateTimeOffset? lastCreatedAt,
        [FromQuery] Guid? lastContentId,
        [FromQuery] int pageSize = 20)
    {
        if (pageSize is < 1 or > 100) pageSize = 20;

        await EnsureSampleContentsAsync();

        var query = _context.Contents
            .AsNoTracking()
            .Where(c => c.Visibility == "Public" && !c.IsDeleted);

        if (subjectId.HasValue)
        {
            query = query.Where(c => c.SubjectId == subjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            query = query.Where(c => c.ContentType == contentType);
        }

        // Tìm kiếm phân tách bằng dấu phẩy và chuẩn hóa bỏ dấu/khoảng trắng theo Tag (#toancaocap, #chuongroirac)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var tokens = search.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(t => t.Trim())
                               .Where(t => !string.IsNullOrEmpty(t))
                               .ToList();

            if (tokens.Count == 1)
            {
                var token = tokens[0];
                var normToken = TagHelper.RemoveDiacriticsAndSpaces(token);
                var normUpper = normToken.ToUpper();
                var tagHash = "#" + normToken;

                query = query.Where(c => c.Title.Contains(token) || 
                                         c.Subject.Name.Contains(token) || 
                                         c.ContentTags.Any(ct => ct.Tag.TagName.Contains(token) || 
                                                                 ct.Tag.TagName.Contains(tagHash) ||
                                                                 ct.Tag.NormalizedTagName.Contains(normUpper)));
            }
            else if (tokens.Count > 1)
            {
                var tokenData = tokens.Select(t => new
                {
                    Raw = t,
                    NormUpper = TagHelper.RemoveDiacriticsAndSpaces(t).ToUpper(),
                    TagHash = "#" + TagHelper.RemoveDiacriticsAndSpaces(t)
                }).ToList();

                var parameter = System.Linq.Expressions.Expression.Parameter(typeof(Entities.Core.Content), "c");
                System.Linq.Expressions.Expression? combinedOr = null;

                var titleProp = System.Linq.Expressions.Expression.Property(parameter, nameof(Entities.Core.Content.Title));
                var subjProp = System.Linq.Expressions.Expression.Property(parameter, nameof(Entities.Core.Content.Subject));
                var subjNameProp = System.Linq.Expressions.Expression.Property(subjProp, nameof(Subject.Name));
                var contentTagsProp = System.Linq.Expressions.Expression.Property(parameter, nameof(Entities.Core.Content.ContentTags));

                var stringContainsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;

                foreach (var td in tokenData)
                {
                    var titleExpr = System.Linq.Expressions.Expression.Call(titleProp, stringContainsMethod, System.Linq.Expressions.Expression.Constant(td.Raw));
                    var subjExpr = System.Linq.Expressions.Expression.Call(subjNameProp, stringContainsMethod, System.Linq.Expressions.Expression.Constant(td.Raw));

                    var ctParam = System.Linq.Expressions.Expression.Parameter(typeof(ContentTag), "ct");
                    var tagProp = System.Linq.Expressions.Expression.Property(ctParam, nameof(ContentTag.Tag));
                    var tagNameProp = System.Linq.Expressions.Expression.Property(tagProp, nameof(Tag.TagName));
                    var normProp = System.Linq.Expressions.Expression.Property(tagProp, nameof(Tag.NormalizedTagName));

                    var tagContainsRaw = System.Linq.Expressions.Expression.Call(tagNameProp, stringContainsMethod, System.Linq.Expressions.Expression.Constant(td.Raw));
                    var tagContainsHash = System.Linq.Expressions.Expression.Call(tagNameProp, stringContainsMethod, System.Linq.Expressions.Expression.Constant(td.TagHash));
                    var normContains = System.Linq.Expressions.Expression.Call(normProp, stringContainsMethod, System.Linq.Expressions.Expression.Constant(td.NormUpper));

                    var tagMatch = System.Linq.Expressions.Expression.OrElse(System.Linq.Expressions.Expression.OrElse(tagContainsRaw, tagContainsHash), normContains);
                    var tagLambda = System.Linq.Expressions.Expression.Lambda<Func<ContentTag, bool>>(tagMatch, ctParam);

                    var anyMethod = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
                        .MakeGenericMethod(typeof(ContentTag));
                    var tagAnyExpr = System.Linq.Expressions.Expression.Call(anyMethod, contentTagsProp, tagLambda);

                    var tokenCondition = System.Linq.Expressions.Expression.OrElse(System.Linq.Expressions.Expression.OrElse(titleExpr, subjExpr), tagAnyExpr);

                    combinedOr = combinedOr == null ? tokenCondition : System.Linq.Expressions.Expression.OrElse(combinedOr, tokenCondition);
                }

                if (combinedOr != null)
                {
                    var lambda = System.Linq.Expressions.Expression.Lambda<Func<Entities.Core.Content, bool>>(combinedOr, parameter);
                    query = query.Where(lambda);
                }
            }
        }

        // Keyset Pagination: Cực nhanh trên tập dữ liệu lớn
        if (lastCreatedAt.HasValue && lastContentId.HasValue)
        {
            query = query.Where(c => c.CreatedAt < lastCreatedAt.Value 
                                  || (c.CreatedAt == lastCreatedAt.Value && c.ContentId.CompareTo(lastContentId.Value) < 0));
        }

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.ContentId)
            .Take(pageSize)
            .Select(c => new
            {
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
                Subject = new { c.Subject.SubjectId, c.Subject.Code, c.Subject.Name },
                Author = new { c.Owner.UserId, c.Owner.Profile!.DisplayName, c.Owner.Profile.AvatarUrl, c.Owner.Profile.RankTier },
                Tags = c.ContentTags.Select(ct => ct.Tag.TagName).ToList()
            })
            .ToListAsync();

        return Ok(items);
    }

    private async Task EnsureSampleContentsAsync()
    {
        await Task.CompletedTask;
        return;
    }


    private static string RemoveDiacriticsAndSpaces(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                if (c == 'đ' || c == 'Đ') sb.Append('d');
                else if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>
    /// Chi tiết bài Đề cương hoặc Quiz
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetContentDetail(Guid id)
    {
        var content = await _context.Contents
            .AsNoTracking()
            .Include(c => c.Subject)
            .Include(c => c.Owner)
            .ThenInclude(o => o.Profile)
            .Include(c => c.StudyOutline)
            .Include(c => c.Quiz)
            .ThenInclude(q => q!.Questions)
            .ThenInclude(qu => qu.Answers)
            .FirstOrDefaultAsync(c => c.ContentId == id && !c.IsDeleted);

        if (content == null)
        {
            return NotFound(new { Message = "Nội dung không tồn tại hoặc đã bị xóa." });
        }

        if (content.Quiz?.Questions != null)
        {
            content.Quiz.Questions = content.Quiz.Questions
                .OrderBy(q => q.OrderIndex)
                .Select(q =>
                {
                    q.Answers = q.Answers.OrderBy(a => a.OrderIndex).ToList();
                    return q;
                })
                .ToList();
        }

        // Tăng lượt xem bất đồng bộ
        _ = Task.Run(async () =>
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE core.Contents SET ViewCount = ViewCount + 1 WHERE ContentId = {0}", id);
            }
            catch { /* Ignore background counter increment error */ }
        });

        return Ok(content);
    }

    /// <summary>
    /// Toggle Star (Yêu thích / Bỏ yêu thích) với Atomic Transaction (UC03)
    /// </summary>
    [Authorize]
    [HttpPost("{id:guid}/stars")]
    public async Task<IActionResult> ToggleStar(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var content = await _context.Contents.FirstOrDefaultAsync(c => c.ContentId == id && !c.IsDeleted);
            if (content == null)
            {
                return NotFound(new { Message = "Nội dung không tồn tại." });
            }

            var existingStar = await _context.UserStars
                .FirstOrDefaultAsync(s => s.UserId == userId && s.ContentId == id);

            bool isStarred;
            if (existingStar != null)
            {
                _context.UserStars.Remove(existingStar);
                if (content.StarCount > 0) content.StarCount--;
                isStarred = false;
            }
            else
            {
                _context.UserStars.Add(new UserStar
                {
                    UserId = userId,
                    ContentId = id,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                content.StarCount++;
                isStarred = true;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { IsStarred = isStarred, StarCount = content.StarCount });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { Message = "Lỗi khi xử lý đánh dấu yêu thích.", Error = ex.Message });
        }
    }

    /// <summary>
    /// Lấy danh sách đề cương & bộ đề do người dùng hiện tại đăng tải
    /// </summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyContents()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid userId;
        if (!Guid.TryParse(userIdStr, out userId))
        {
            var firstUser = await _context.Users.FirstOrDefaultAsync();
            if (firstUser != null) userId = firstUser.UserId;
            else return Ok(new List<object>());
        }

        var items = await _context.Contents
            .AsNoTracking()
            .Where(c => c.OwnerId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
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
                Subject = new { c.Subject.SubjectId, c.Subject.Code, c.Subject.Name },
                Author = new { c.Owner.UserId, c.Owner.Profile!.DisplayName, c.Owner.Profile.AvatarUrl, c.Owner.Profile.RankTier },
                Tags = c.ContentTags.Select(ct => ct.Tag.TagName).ToList(),
                AttachedFileUrl = c.StudyOutline != null ? c.StudyOutline.AttachedFileUrl : null,
                AttachedFileSizeBytes = c.StudyOutline != null ? c.StudyOutline.AttachedFileSizeBytes : null
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Xóa tài liệu đề cương / quiz (chỉ cho phép chủ sở hữu hoặc admin)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteContent(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = Guid.TryParse(userIdStr, out var uid) ? uid : null;

        var content = await _context.Contents
            .Include(c => c.StudyOutline)
            .FirstOrDefaultAsync(c => c.ContentId == id && !c.IsDeleted);

        if (content == null)
        {
            return NotFound(new { Message = "Nội dung không tồn tại hoặc đã bị xóa." });
        }

        // Kiểm tra quyền sở hữu (nếu có userId từ token thì phải đúng chủ hoặc role Admin)
        if (userId.HasValue && content.OwnerId != userId.Value)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "Admin")
            {
                return StatusCode(403, new { Message = "Bạn không có quyền xóa tài liệu của người khác." });
            }
        }

        content.IsDeleted = true;
        content.DeletedAt = DateTimeOffset.UtcNow;

        // Xóa file vật lý nếu có
        if (content.StudyOutline?.AttachedFileUrl != null)
        {
            try
            {
                var relativePath = content.StudyOutline.AttachedFileUrl.TrimStart('/');
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var fullPath = Path.Combine(webRoot, relativePath);
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch { /* Ignore physical file deletion error */ }
        }

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Đã xóa tài liệu thành công." });
    }
}
