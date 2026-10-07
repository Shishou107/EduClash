using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ui_quamonhai.Services;

namespace ui_quamonhai.Controllers;

public class OutlineController : Controller
{
    private readonly IEduClashApiClient _apiClient;
    private readonly IAuthService _authService;

    public OutlineController(IEduClashApiClient apiClient, IAuthService authService)
    {
        _apiClient = apiClient;
        _authService = authService;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Import()
    {
        var subjects = await _apiClient.GetSubjectsAsync();
        ViewBag.Subjects = subjects;
        return View();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        IFormFile? file, 
        string title, 
        int subjectId, 
        string? mode, 
        string? difficultyLevel, 
        string? tags, 
        string? description)
    {
        var subjects = await _apiClient.GetSubjectsAsync();
        ViewBag.Subjects = subjects;

        if (file == null || file.Length == 0)
        {
            ViewBag.ErrorMessage = "Vui lòng chọn một tập tin DOCX hoặc PDF.";
            return View();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            ViewBag.ErrorMessage = "Vui lòng nhập tiêu đề tài liệu.";
            return View();
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".docx" && ext != ".pdf")
        {
            ViewBag.ErrorMessage = "Hệ thống chỉ hỗ trợ định dạng Microsoft Word (.docx) và Adobe PDF (.pdf).";
            return View();
        }

        var docMode = string.Equals(mode, "StructuredQuiz", StringComparison.OrdinalIgnoreCase) 
            ? "StructuredQuiz" 
            : "TheoryOutline";

        // Tự động thêm tag phân loại
        var finalTags = tags?.Trim() ?? "";
        if (docMode == "StructuredQuiz")
        {
            if (!finalTags.Contains("#tracnghiem", StringComparison.OrdinalIgnoreCase))
            {
                finalTags = string.IsNullOrWhiteSpace(finalTags) ? "#tracnghiem" : finalTags + ", #tracnghiem";
            }
        }
        else
        {
            if (!finalTags.Contains("#lythuyet", StringComparison.OrdinalIgnoreCase))
            {
                finalTags = string.IsNullOrWhiteSpace(finalTags) ? "#lythuyet" : finalTags + ", #lythuyet";
            }
        }

        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(title), "title");
            form.Add(new StringContent(subjectId.ToString()), "subjectId");
            form.Add(new StringContent(docMode), "mode");
            form.Add(new StringContent(difficultyLevel ?? "Medium"), "difficultyLevel");
            form.Add(new StringContent(finalTags), "tags");
            if (!string.IsNullOrEmpty(description)) form.Add(new StringContent(description), "description");

            using var stream = file.OpenReadStream();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            form.Add(fileContent, "file", file.FileName);

            var (success, message, contentId, resultMode, fileUrl) = await _apiClient.UploadOutlineAsync(form);
            if (success)
            {
                TempData["SuccessMessage"] = message;

                // Nếu tạo thành công có ID, mở xem ngay tài liệu đó
                if (contentId.HasValue)
                {
                    if (string.Equals(resultMode ?? docMode, "StructuredQuiz", StringComparison.OrdinalIgnoreCase))
                    {
                        return RedirectToAction("Quiz", "Home", new { id = contentId.Value });
                    }
                    else
                    {
                        return RedirectToAction("Outline", "Home", new { id = contentId.Value });
                    }
                }

                // Fallback nếu không có ID
                if (string.Equals(docMode, "StructuredQuiz", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Quizzed", "Home", new { subjectId = subjectId });
                }
                return RedirectToAction("Index", "Home", new { subjectId = subjectId });
            }

            ViewBag.ErrorMessage = message;
            return View();
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = $"Lỗi xử lý file: {ex.Message}";
            return View();
        }
    }

    /// <summary>
    /// Trang quản lý đề cương & bài trắc nghiệm của tôi
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MyOutlines(string? search, string? type)
    {
        var myContents = await _apiClient.GetMyContentsAsync();

        if (!string.IsNullOrWhiteSpace(type) && type != "All")
        {
            myContents = myContents.Where(c => c.ContentType.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            myContents = myContents.Where(c => c.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                                            || (c.Subject?.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                                            || (c.Subject?.Code?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }

        ViewBag.Search = search ?? "";
        ViewBag.Type = type ?? "All";
        return View(myContents);
    }

    /// <summary>
    /// Xóa tài liệu đề cương / trắc nghiệm (Form Submit)
    /// </summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var (success, message) = await _apiClient.DeleteContentAsync(id);
        if (success)
        {
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }
        return RedirectToAction(nameof(MyOutlines));
    }

    /// <summary>
    /// Xóa tài liệu qua AJAX (không reload trang)
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> DeleteAjax([FromRoute] Guid? id, [FromForm] Guid? formId)
    {
        var targetId = id ?? formId;
        if (!targetId.HasValue || targetId.Value == Guid.Empty)
        {
            return BadRequest(new { success = false, message = "ID tài liệu không hợp lệ." });
        }

        var (success, message) = await _apiClient.DeleteContentAsync(targetId.Value);
        return Json(new { success, message });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleStar([FromRoute] Guid id)
    {
        var result = await _apiClient.ToggleStarAsync(id);
        return Json(new { success = true, isStarred = result.isStarred, newCount = result.newCount });
    }

    [HttpPost]
    public async Task<IActionResult> GenerateAiQuiz(Guid id, int coins = 3, int? count = null)
    {
        var result = await _apiClient.GenerateAiQuizAsync(id, coins, count);
        return Json(new { success = result.success, message = result.message, remainingCoins = result.remainingCoins });
    }
}
