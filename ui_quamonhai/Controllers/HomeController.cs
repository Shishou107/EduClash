using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ui_quamonhai.Models;
using ui_quamonhai.Services;

namespace ui_quamonhai.Controllers;

public class HomeController : Controller
{
    private readonly IEduClashApiClient _apiClient;
    private readonly IAuthService _authService;

    public HomeController(IEduClashApiClient apiClient, IAuthService authService)
    {
        _apiClient = apiClient;
        _authService = authService;
    }

    public async Task<IActionResult> Index(int? subjectId, string? search)
    {
        var subjectsTask = _apiClient.GetSubjectsAsync();
        var contentsTask = _apiClient.GetPublicContentsAsync(subjectId: subjectId, contentType: "StudyOutline", search: search);

        await Task.WhenAll(subjectsTask, contentsTask);
        var subjects = await subjectsTask;
        var contents = await contentsTask;

        // Cập nhật số đề cương
        foreach (var s in subjects)
        {
            s.ContentsCount = contents.Count(c => c.Subject.SubjectId == s.SubjectId);
        }

        ViewBag.SelectedSubjectId = subjectId;
        ViewBag.SearchKeyword = search ?? "";
        ViewBag.Subjects = subjects;
        ViewBag.SelectedSubject = subjects.FirstOrDefault(s => s.SubjectId == subjectId);

        return View(contents);
    }

    public async Task<IActionResult> Subjects(string? search)
    {
        var subjects = await _apiClient.GetSubjectsAsync();
        if (!string.IsNullOrWhiteSpace(search))
        {
            subjects = subjects.Where(s => s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) 
                                        || s.Code.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        ViewBag.SearchKeyword = search ?? "";
        return View(subjects);
    }

    public async Task<IActionResult> Quizzed(int? subjectId, string? search)
    {
        var subjectsTask = _apiClient.GetSubjectsAsync();
        var quizzesTask = _apiClient.GetPublicContentsAsync(subjectId: subjectId, contentType: "Quiz", search: search);

        await Task.WhenAll(subjectsTask, quizzesTask);
        var subjects = await subjectsTask;
        var quizzes = await quizzesTask;

        ViewBag.SelectedSubjectId = subjectId;
        ViewBag.SearchKeyword = search ?? "";
        ViewBag.Subjects = subjects;
        ViewBag.SelectedSubject = subjects.FirstOrDefault(s => s.SubjectId == subjectId);

        return View(quizzes);
    }

    public async Task<IActionResult> Outline(Guid id)
    {
        var outline = await _apiClient.GetOutlineDetailAsync(id);
        if (outline != null)
        {
            return View("OutlineDetail", outline);
        }

        // Tự động kiểm tra nếu đây là bộ đề trắc nghiệm thì chuyển hướng sang Quiz
        var quiz = await _apiClient.GetQuizDetailAsync(id);
        if (quiz != null)
        {
            return RedirectToAction("Quiz", new { id });
        }

        return NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> DocumentProxy(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return BadRequest("Url is required");
        var stream = await _apiClient.GetFileStreamAsync(url);
        if (stream == null) return NotFound();

        var ext = Path.GetExtension(url).ToLowerInvariant();
        var contentType = ext == ".pdf" ? "application/pdf" : ext == ".docx" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "application/octet-stream";
        return File(stream, contentType);
    }

    public async Task<IActionResult> Quiz(Guid id)
    {
        var quiz = await _apiClient.GetQuizDetailAsync(id);
        if (quiz != null)
        {
            return View("QuizDetail", quiz);
        }

        // Tự động kiểm tra nếu đây là đề cương lý thuyết thì chuyển hướng sang Outline
        var outline = await _apiClient.GetOutlineDetailAsync(id);
        if (outline != null)
        {
            return RedirectToAction("Outline", new { id });
        }

        return NotFound();
    }

    public async Task<IActionResult> Leaderboard(bool byRating = true)
    {
        var board = await _apiClient.GetLeaderboardAsync(byRating);
        ViewBag.ByRating = byRating;
        return View(board);
    }

    public async Task<IActionResult> Arena()
    {
        var subjectsTask = _apiClient.GetSubjectsAsync();
        var quizzesTask = _apiClient.GetPublicContentsAsync(contentType: "Quiz");
        var myContentsTask = _apiClient.GetMyContentsAsync();
        await Task.WhenAll(subjectsTask, quizzesTask, myContentsTask);

        var allQuizzes = (await quizzesTask).ToList();
        var myQuizzes = (await myContentsTask).Where(c => c.ContentType == "Quiz").ToList();
        foreach (var mq in myQuizzes)
        {
            if (!allQuizzes.Any(q => q.ContentId == mq.ContentId))
            {
                allQuizzes.Add(mq);
            }
        }

        ViewBag.Subjects = await subjectsTask;
        ViewBag.Quizzes = allQuizzes;
        ViewBag.CurrentUser = _authService.CurrentUser;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetQuizQuestions(Guid id)
    {
        var questions = await _apiClient.GetQuizQuestionsAsync(id);
        return Json(questions);
    }

    [HttpPost]
    public async Task<IActionResult> CreateArenaRoom([FromBody] CreateRoomRequest req)
    {
        var user = _authService.CurrentUser;
        if (user != null)
        {
            if (string.IsNullOrWhiteSpace(req.PlayerId)) req.PlayerId = user.UserId.ToString();
            if (string.IsNullOrWhiteSpace(req.DisplayName)) req.DisplayName = user.DisplayName;
            req.Rating = user.Rating;
            req.Level = user.Level;
            req.RankTier = user.RankTier;
            req.AvatarUrl = user.AvatarUrl;
        }

        var room = await _apiClient.CreateArenaRoomAsync(req);
        if (room == null) return BadRequest(new { Message = "Không thể tạo phòng đấu trên máy chủ." });
        return Json(room);
    }

    [HttpPost]
    public async Task<IActionResult> JoinArenaRoom([FromBody] JoinRoomRequest req)
    {
        var user = _authService.CurrentUser;
        if (user != null)
        {
            if (string.IsNullOrWhiteSpace(req.PlayerId)) req.PlayerId = user.UserId.ToString();
            if (string.IsNullOrWhiteSpace(req.DisplayName)) req.DisplayName = user.DisplayName;
            req.Rating = user.Rating;
            req.Level = user.Level;
            req.RankTier = user.RankTier;
            req.AvatarUrl = user.AvatarUrl;
        }

        var (success, message, room) = await _apiClient.JoinArenaRoomAsync(req);
        if (!success || room == null) return BadRequest(new { Message = message });
        return Json(room);
    }

    [HttpGet]
    public async Task<IActionResult> GetArenaRoom(string code)
    {
        var room = await _apiClient.GetArenaRoomAsync(code);
        if (room == null) return NotFound(new { Message = "Phòng không tồn tại." });
        return Json(room);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitArenaAnswer([FromBody] SubmitAnswerRequest req)
    {
        var room = await _apiClient.SubmitArenaAnswerAsync(req);
        if (room == null) return BadRequest();
        return Json(room);
    }

    [HttpPost]
    public async Task<IActionResult> LeaveArenaRoom(string code, [FromQuery] string playerId)
    {
        await _apiClient.LeaveArenaRoomAsync(code, playerId);
        return Ok(new { Message = "Đã rời phòng." });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
