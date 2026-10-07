using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ui_quamonhai.Models;

namespace ui_quamonhai.Services;

public interface IEduClashApiClient
{
    Task<List<ContentItemViewModel>> GetPublicContentsAsync(int? subjectId = null, string? contentType = null, string? search = null);
    Task<List<SubjectViewModel>> GetSubjectsAsync();
    Task<List<LeaderboardItemViewModel>> GetLeaderboardAsync(bool byRating = true);
    Task<UserProfileViewModel?> GetCurrentProfileAsync();
    Task<(bool isStarred, int newCount)> ToggleStarAsync(Guid contentId);
    Task<OutlineDetailViewModel?> GetOutlineDetailAsync(Guid id);
    Task<(bool success, string message, Guid? contentId, string? mode, string? fileUrl)> UploadOutlineAsync(MultipartFormDataContent content);
    Task<(bool success, string message, int remainingCoins)> GenerateAiQuizAsync(Guid outlineId, int coins = 3, int? count = null);
    Task<List<QuestionViewModel>> GetQuizQuestionsAsync(Guid contentId);
    Task<QuizDetailViewModel?> GetQuizDetailAsync(Guid contentId);
    Task<List<ContentItemViewModel>> GetMyContentsAsync();
    Task<(bool success, string message)> DeleteContentAsync(Guid contentId);
    Task<ArenaRoomDto?> CreateArenaRoomAsync(CreateRoomRequest req);
    Task<(bool success, string message, ArenaRoomDto? room)> JoinArenaRoomAsync(JoinRoomRequest req);
    Task<ArenaRoomDto?> GetArenaRoomAsync(string roomCode);
    Task<ArenaRoomDto?> SubmitArenaAnswerAsync(SubmitAnswerRequest req);
    Task LeaveArenaRoomAsync(string roomCode, string playerId);
    Task<Stream?> GetFileStreamAsync(string url);
}

public class EduClashApiClient : IEduClashApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    // In-memory caching để chuyển tab tức thì (0ms) không phải chờ gọi mạng Somee mỗi lần
    private static List<SubjectViewModel>? _cachedSubjects;
    private static DateTime _subjectsCacheTime = DateTime.MinValue;
    private static readonly Dictionary<string, (DateTime timestamp, List<ContentItemViewModel> data)> _contentsCache = new();
    private static readonly Dictionary<string, (DateTime timestamp, List<LeaderboardItemViewModel> data)> _leaderboardCache = new();

    public EduClashApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
    }

    private void SetAuthHeader()
    {
        var token = _httpContextAccessor.HttpContext?.User?.FindFirst("AccessToken")?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<List<ContentItemViewModel>> GetPublicContentsAsync(int? subjectId = null, string? contentType = null, string? search = null)
    {
        var cacheKey = $"{subjectId}_{contentType}_{search}";
        lock (_contentsCache)
        {
            if (_contentsCache.TryGetValue(cacheKey, out var cache) && (DateTime.UtcNow - cache.timestamp).TotalSeconds < 25)
            {
                return cache.data;
            }
        }

        try
        {
            var url = "api/v1/contents";
            var queryParams = new List<string>();
            queryParams.Add("pageSize=100");
            if (subjectId.HasValue) queryParams.Add($"subjectId={subjectId.Value}");
            if (!string.IsNullOrEmpty(contentType)) queryParams.Add($"contentType={contentType}");
            if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");
            if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2.5));
            var result = await _http.GetFromJsonAsync<List<ContentItemViewModel>>(url, cts.Token);
            if (result != null)
            {
                lock (_contentsCache)
                {
                    _contentsCache[cacheKey] = (DateTime.UtcNow, result);
                }
                return result;
            }
        }
        catch
        {
            // API offline hoặc timeout -> nếu có cache cũ thì trả về ngay
            lock (_contentsCache)
            {
                if (_contentsCache.TryGetValue(cacheKey, out var oldCache))
                {
                    return oldCache.data;
                }
            }
        }

        return new List<ContentItemViewModel>();
    }

    public async Task<List<SubjectViewModel>> GetSubjectsAsync()
    {
        if (_cachedSubjects != null && _cachedSubjects.Count > 0 && (DateTime.UtcNow - _subjectsCacheTime).TotalMinutes < 30)
        {
            return _cachedSubjects;
        }

        var defaultList = GetDefaultSubjects();
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
            var result = await _http.GetFromJsonAsync<List<SubjectViewModel>>("api/v1/subjects", cts.Token);
            if (result != null && result.Count > 0)
            {
                foreach (var s in result)
                {
                    var match = defaultList.FirstOrDefault(d => d.Code.Equals(s.Code, StringComparison.OrdinalIgnoreCase) 
                                                             || d.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        s.ThemeColor = match.ThemeColor;
                        s.TextColor = match.TextColor;
                        if (string.IsNullOrEmpty(s.Description)) s.Description = match.Description;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(s.ThemeColor)) s.ThemeColor = "#0284C7";
                        if (string.IsNullOrEmpty(s.TextColor)) s.TextColor = "#FFFFFF";
                    }
                }

                // Giữ đầy đủ danh mục môn học chuẩn
                foreach (var d in defaultList)
                {
                    if (!result.Any(r => r.Code.Equals(d.Code, StringComparison.OrdinalIgnoreCase) || r.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Add(d);
                    }
                }

                _cachedSubjects = result;
                _subjectsCacheTime = DateTime.UtcNow;
                return result;
            }
        }
        catch
        {
            // API timeout hoặc offline -> trả về danh sách môn chuẩn ngay lập tức
        }

        _cachedSubjects = defaultList;
        _subjectsCacheTime = DateTime.UtcNow;
        return defaultList;
    }

    private static List<SubjectViewModel> GetDefaultSubjects()
    {
        return new List<SubjectViewModel>
        {
            new() { SubjectId = 1, Code = "CS101", Name = "Nhập môn Lập trình", Description = "Khái niệm biến, kiểu dữ liệu, vòng lặp và hàm cơ bản", ThemeColor = "#0284C7", TextColor = "#FFFFFF" },
            new() { SubjectId = 2, Code = "DB201", Name = "Hệ Quản trị CSDL", Description = "Mô hình quan hệ ERD, chuẩn hóa 3NF, truy vấn SQL và ACID", ThemeColor = "#065F46", TextColor = "#FFFFFF" },
            new() { SubjectId = 3, Code = "SE301", Name = "Phân tích Thiết kế Hệ thống", Description = "UML, Use Case, ERD, RESTful API và Kiến trúc phần mềm", ThemeColor = "#0D9488", TextColor = "#FFFFFF" },
            new() { SubjectId = 4, Code = "KTMT201", Name = "Kiến trúc máy tính", Description = "Tập lệnh ISA, vi kiến trúc CPU, bộ nhớ Cache và pipeline", ThemeColor = "#0369A1", TextColor = "#FFFFFF" },
            new() { SubjectId = 5, Code = "LLCT101", Name = "Triết học Mác - Lênin", Description = "Thế giới quan và phương pháp luận triết học Mác - Lênin", ThemeColor = "#701A1A", TextColor = "#FFFFFF" },
            new() { SubjectId = 6, Code = "PLDC101", Name = "Pháp luật đại cương", Description = "Kiến thức cơ bản về nhà nước và hệ thống pháp luật Việt Nam", ThemeColor = "#B45309", TextColor = "#FFFFFF" },
            new() { SubjectId = 7, Code = "KTLT102", Name = "Kỹ thuật lập trình", Description = "Con trỏ, cấp phát động, đệ quy, kỹ thuật tối ưu hóa mã nguồn", ThemeColor = "#1D4ED8", TextColor = "#FFFFFF" },
            new() { SubjectId = 8, Code = "MATH202", Name = "Toán rời rạc", Description = "Logic mệnh đề, tập hợp, quan hệ, lý thuyết đồ thị và tổ hợp", ThemeColor = "#4338CA", TextColor = "#FFFFFF" },
            new() { SubjectId = 9, Code = "NET201", Name = "Mạng máy tính", Description = "Mô hình OSI, TCP/IP, địa chỉ IP, Subnet và các giao thức mạng", ThemeColor = "#0F766E", TextColor = "#FFFFFF" },
            new() { SubjectId = 10, Code = "CS201", Name = "Cấu trúc dữ liệu & GT", Description = "Danh sách liên kết, ngăn xếp Stack, hàng đợi Queue, cây nhị phân", ThemeColor = "#164E63", TextColor = "#FFFFFF" },
            new() { SubjectId = 11, Code = "OS201", Name = "Hệ điều hành", Description = "Quản lý tiến trình Process, luồng Thread, bộ nhớ ảo và Deadlock", ThemeColor = "#334155", TextColor = "#FFFFFF" },
            new() { SubjectId = 12, Code = "ATTT301", Name = "An toàn thông tin", Description = "Mã hóa đối xứng/bất đối xứng, chữ ký số, tường lửa và lỗ hổng mạng", ThemeColor = "#991B1B", TextColor = "#FFFFFF" },
            new() { SubjectId = 13, Code = "MATH101", Name = "Toán cao cấp", Description = "Giải tích hàm một biến, ma trận định thức, đại số tuyến tính", ThemeColor = "#312E81", TextColor = "#FFFFFF" },
            new() { SubjectId = 14, Code = "LLCT102", Name = "Tư tưởng Hồ Chí Minh", Description = "Hệ thống quan điểm toàn diện và sâu sắc về cách mạng Việt Nam", ThemeColor = "#9A3412", TextColor = "#FFFFFF" },
            new() { SubjectId = 15, Code = "LLCT103", Name = "Kinh tế chính trị Mác - Lênin", Description = "Học thuyết giá trị thặng dư, quy luật kinh tế thị trường và hội nhập", ThemeColor = "#831843", TextColor = "#FFFFFF" },
            new() { SubjectId = 16, Code = "LLCT104", Name = "Lịch sử Đảng Cộng sản VN", Description = "Sự ra đời của Đảng, quá trình lãnh đạo cách mạng và thời kỳ đổi mới", ThemeColor = "#7F1D1D", TextColor = "#FFFFFF" },
            new() { SubjectId = 17, Code = "XHH101", Name = "Xã hội học", Description = "Các quy luật và hình thái phát triển xã hội", ThemeColor = "#1E293B", TextColor = "#FFFFFF" },
            new() { SubjectId = 18, Code = "ENG101", Name = "Tiếng Anh chuyên ngành", Description = "Từ vựng học thuật, kỹ năng đọc hiểu tài liệu và dịch thuật kỹ thuật", ThemeColor = "#4C1D95", TextColor = "#FFFFFF" }
        };
    }

    public async Task<List<LeaderboardItemViewModel>> GetLeaderboardAsync(bool byRating = true)
    {
        var cacheKey = byRating ? "rating" : "xp";
        lock (_leaderboardCache)
        {
            if (_leaderboardCache.TryGetValue(cacheKey, out var cache) && (DateTime.UtcNow - cache.timestamp).TotalSeconds < 30)
            {
                return cache.data;
            }
        }

        try
        {
            var endpoint = byRating ? "api/v1/leaderboards/rating" : "api/v1/leaderboards/xp";
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2.5));
            var result = await _http.GetFromJsonAsync<List<LeaderboardItemViewModel>>(endpoint, cts.Token);
            if (result != null)
            {
                int rank = 1;
                foreach (var item in result) item.RankPosition = rank++;
                lock (_leaderboardCache)
                {
                    _leaderboardCache[cacheKey] = (DateTime.UtcNow, result);
                }
                return result;
            }
        }
        catch
        {
            // API offline -> trả về cache cũ nếu có
            lock (_leaderboardCache)
            {
                if (_leaderboardCache.TryGetValue(cacheKey, out var oldCache))
                {
                    return oldCache.data;
                }
            }
        }

        return new List<LeaderboardItemViewModel>();
    }

    public async Task<UserProfileViewModel?> GetCurrentProfileAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<UserProfileViewModel>("api/v1/auth/me");
            if (result != null) return result;
        }
        catch
        {
            // Chưa đăng nhập hoặc API offline
        }

        return null;
    }

    public async Task<(bool isStarred, int newCount)> ToggleStarAsync(Guid contentId)
    {
        try
        {
            var res = await _http.PostAsync($"api/v1/contents/{contentId}/stars", null);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<StarResultDto>();
                if (body != null) return (body.IsStarred, body.StarCount);
            }
        }
        catch
        {
            // Offline
        }

        return (true, 0);
    }

    public async Task<OutlineDetailViewModel?> GetOutlineDetailAsync(Guid id)
    {
        // 1. Thử gọi api/v1/study-outlines/{id}
        try
        {
            var res = await _http.GetAsync($"api/v1/study-outlines/{id}");
            if (res.IsSuccessStatusCode)
            {
                var result = await res.Content.ReadFromJsonAsync<OutlineDetailViewModel>();
                if (result != null && !string.IsNullOrEmpty(result.Title))
                {
                    if (!string.IsNullOrEmpty(result.AttachedFileUrl) && !result.AttachedFileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        var baseUri = _http.BaseAddress?.ToString().TrimEnd('/') ?? "";
                        result.AttachedFileUrl = $"{baseUri}/{result.AttachedFileUrl.TrimStart('/')}";
                    }
                    result.Sections = new List<OutlineSectionItemViewModel>();
                    return result;
                }
            }
        }
        catch { }

        // 2. Fallback gọi api/v1/contents/{id} và bóc tách dữ liệu
        try
        {
            var res = await _http.GetAsync($"api/v1/contents/{id}");
            if (res.IsSuccessStatusCode)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                
                var vm = new OutlineDetailViewModel
                {
                    ContentId = id,
                    Title = root.TryGetProperty("title", out var tp) || root.TryGetProperty("Title", out tp) ? tp.GetString() ?? "" : "",
                    Description = root.TryGetProperty("description", out var dp) || root.TryGetProperty("Description", out dp) ? dp.GetString() : null,
                    DifficultyLevel = root.TryGetProperty("difficultyLevel", out var dlp) || root.TryGetProperty("DifficultyLevel", out dlp) ? dlp.GetString() ?? "Medium" : "Medium",
                    StarCount = root.TryGetProperty("starCount", out var scp) || root.TryGetProperty("StarCount", out scp) ? scp.GetInt32() : 0,
                    ViewCount = root.TryGetProperty("viewCount", out var vcp) || root.TryGetProperty("ViewCount", out vcp) ? vcp.GetInt32() : 0
                };

                if (root.TryGetProperty("subject", out var sp) || root.TryGetProperty("Subject", out sp))
                {
                    vm.Subject = new SubjectBriefViewModel
                    {
                        SubjectId = sp.TryGetProperty("subjectId", out var sidp) || sp.TryGetProperty("SubjectId", out sidp) ? sidp.GetInt32() : 0,
                        Code = sp.TryGetProperty("code", out var scodep) || sp.TryGetProperty("Code", out scodep) ? scodep.GetString() ?? "" : "",
                        Name = sp.TryGetProperty("name", out var snamep) || sp.TryGetProperty("Name", out snamep) ? snamep.GetString() ?? "" : ""
                    };
                }

                if (root.TryGetProperty("owner", out var op) || root.TryGetProperty("Owner", out op))
                {
                    vm.OwnerId = op.TryGetProperty("userId", out var uidp) || op.TryGetProperty("UserId", out uidp) ? uidp.GetGuid() : Guid.Empty;
                    if (op.TryGetProperty("profile", out var profp) || op.TryGetProperty("Profile", out profp))
                    {
                        vm.Author = new AuthorBriefViewModel
                        {
                            UserId = vm.OwnerId,
                            DisplayName = profp.TryGetProperty("displayName", out var dnp) || profp.TryGetProperty("DisplayName", out dnp) ? dnp.GetString() ?? "Thành viên" : "Thành viên",
                            AvatarUrl = profp.TryGetProperty("avatarUrl", out var avp) || profp.TryGetProperty("AvatarUrl", out avp) ? avp.GetString() : null
                        };
                    }
                }

                if (root.TryGetProperty("studyOutline", out var sop) || root.TryGetProperty("StudyOutline", out sop))
                {
                    vm.RawContent = sop.TryGetProperty("richTextContent", out var rtcp) || sop.TryGetProperty("RichTextContent", out rtcp) ? rtcp.GetString() ?? "" : "";
                    vm.AttachedFileUrl = sop.TryGetProperty("attachedFileUrl", out var afup) || sop.TryGetProperty("AttachedFileUrl", out afup) ? afup.GetString() : null;
                    if (sop.TryGetProperty("attachedFileSizeBytes", out var fsbp) || sop.TryGetProperty("AttachedFileSizeBytes", out fsbp))
                    {
                        vm.AttachedFileSizeBytes = fsbp.GetInt64();
                    }
                    if (sop.TryGetProperty("tableOfContentsJson", out var tocp) || sop.TryGetProperty("TableOfContentsJson", out tocp))
                    {
                        var tocStr = tocp.GetString();
                        if (!string.IsNullOrWhiteSpace(tocStr))
                        {
                            try { vm.Sections = System.Text.Json.JsonSerializer.Deserialize<List<OutlineSectionItemViewModel>>(tocStr) ?? new(); } catch { }
                        }
                    }
                }

                return vm;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetOutlineDetailAsync] Error: {ex.Message}");
        }

        return null;
    }

    public async Task<(bool success, string message, Guid? contentId, string? mode, string? fileUrl)> UploadOutlineAsync(MultipartFormDataContent content)
    {
        try
        {
            SetAuthHeader();
            var res = await _http.PostAsync("api/v1/study-outlines/upload", content);
            if (res.IsSuccessStatusCode)
            {
                lock (_contentsCache)
                {
                    _contentsCache.Clear(); // Xóa cache để tài liệu mới xuất hiện ngay lập tức
                }
                var body = await res.Content.ReadFromJsonAsync<UploadResultDto>();
                var fileUrl = body?.FileUrl;
                if (!string.IsNullOrEmpty(fileUrl) && !fileUrl.StartsWith("http"))
                {
                    var baseUri = _http.BaseAddress?.ToString().TrimEnd('/') ?? "";
                    fileUrl = $"{baseUri}/{fileUrl.TrimStart('/')}";
                }
                return (true, body?.Message ?? "Tải lên thành công!", body?.ContentId, body?.Mode, fileUrl);
            }
            else
            {
                var err = await res.Content.ReadAsStringAsync();
                return (false, $"Lỗi server: {err}", null, null, null);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối API: {ex.Message}", null, null, null);
        }
    }

    public async Task<(bool success, string message, int remainingCoins)> GenerateAiQuizAsync(Guid outlineId, int coins = 3, int? count = null)
    {
        try
        {
            var c = Math.Max(1, coins);
            var qCount = count.HasValue && count.Value > 0 ? count.Value : c * 2;
            var res = await _http.PostAsync($"api/v1/study-outlines/{outlineId}/generate-ai-quiz?coins={c}&questionCount={qCount}", null);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<AiQuizResultDto>();
                return (true, body?.Message ?? $"AI đã tạo thành công {qCount} câu hỏi!", body?.RemainingCoins ?? 0);
            }
            else
            {
                var err = await res.Content.ReadAsStringAsync();
                return (false, $"Lỗi: {err}", 0);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối: {ex.Message}", 0);
        }
    }

    public async Task<List<QuestionViewModel>> GetQuizQuestionsAsync(Guid contentId)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ContentQuizEnvelopeDto>($"api/v1/contents/{contentId}");
            if (res?.Quiz?.Questions != null && res.Quiz.Questions.Count > 0)
            {
                return res.Quiz.Questions
                    .OrderBy(q => q.OrderIndex)
                    .Select(q => new QuestionViewModel
                    {
                        QuestionId = q.QuestionId,
                        QuestionText = q.QuestionText,
                        QuestionType = q.QuestionType,
                        Points = q.Points,
                        Answers = q.Answers
                            .OrderBy(a => a.OrderIndex)
                            .Select(a => new AnswerViewModel
                            {
                                AnswerId = a.AnswerId,
                                AnswerText = a.AnswerText,
                                IsCorrect = a.IsCorrect
                            }).ToList()
                    }).ToList();
            }
        }
        catch
        {
            // API offline hoặc không có dữ liệu
        }

        return new List<QuestionViewModel>();
    }

    public async Task<QuizDetailViewModel?> GetQuizDetailAsync(Guid contentId)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ContentQuizEnvelopeDto>($"api/v1/contents/{contentId}");
            if (res != null && res.Quiz != null && res.Quiz.Questions != null && res.Quiz.Questions.Count > 0)
            {
                var questions = res.Quiz.Questions
                    .OrderBy(q => q.OrderIndex)
                    .Select(q => new QuestionViewModel
                    {
                        QuestionId = q.QuestionId,
                        QuestionText = q.QuestionText,
                        QuestionType = q.QuestionType,
                        Points = q.Points > 0 ? q.Points : 10,
                        Explanation = q.Explanation,
                        Answers = (q.Answers ?? new List<QuestionAnswerEntryDto>())
                            .OrderBy(a => a.OrderIndex)
                            .Select(a => new AnswerViewModel
                            {
                                AnswerId = a.AnswerId,
                                AnswerText = a.AnswerText,
                                IsCorrect = a.IsCorrect
                            }).ToList()
                    }).ToList();

                var timeLimit = res.Quiz.TimePerQuestionSec > 0
                    ? res.Quiz.TimePerQuestionSec * questions.Count
                    : Math.Max(300, questions.Count * 45);

                return new QuizDetailViewModel
                {
                    QuizId = res.ContentId,
                    Title = string.IsNullOrWhiteSpace(res.Title) ? "Bài thi trắc nghiệm" : res.Title,
                    Description = res.Description ?? string.Empty,
                    Subject = new SubjectBriefViewModel
                    {
                        SubjectId = res.Subject?.SubjectId ?? 0,
                        Code = res.Subject?.Code ?? "",
                        Name = res.Subject?.Name ?? "Trắc nghiệm"
                    },
                    TimeLimitSeconds = timeLimit,
                    Questions = questions
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetQuizDetailAsync] Error: {ex.Message}");
        }

        return null;
    }

    public async Task<List<ContentItemViewModel>> GetMyContentsAsync()
    {
        try
        {
            SetAuthHeader();
            var res = await _http.GetFromJsonAsync<List<ContentItemViewModel>>("api/v1/contents/my");
            if (res != null) return res;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetMyContentsAsync] Error: {ex.Message}");
        }
        return new List<ContentItemViewModel>();
    }

    public async Task<(bool success, string message)> DeleteContentAsync(Guid contentId)
    {
        try
        {
            SetAuthHeader();
            var res = await _http.DeleteAsync($"api/v1/contents/{contentId}");
            if (res.IsSuccessStatusCode)
            {
                lock (_contentsCache)
                {
                    _contentsCache.Clear();
                }
                return (true, "Đã xóa tài liệu thành công!");
            }
            else
            {
                var err = await res.Content.ReadAsStringAsync();
                return (false, $"Lỗi xóa: {err}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<ArenaRoomDto?> CreateArenaRoomAsync(CreateRoomRequest req)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/v1/arena-rooms/create", req);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<ArenaRoomDto>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreateArenaRoomAsync] Error: {ex.Message}");
        }
        return null;
    }

    public async Task<(bool success, string message, ArenaRoomDto? room)> JoinArenaRoomAsync(JoinRoomRequest req)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/v1/arena-rooms/join", req);
            if (res.IsSuccessStatusCode)
            {
                var room = await res.Content.ReadFromJsonAsync<ArenaRoomDto>();
                return (true, "Vào phòng thành công!", room);
            }
            else
            {
                var errObj = await res.Content.ReadFromJsonAsync<SimpleMessageDto>();
                return (false, errObj?.Message ?? "Không thể vào phòng.", null);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối: {ex.Message}", null);
        }
    }

    public async Task<ArenaRoomDto?> GetArenaRoomAsync(string roomCode)
    {
        try
        {
            return await _http.GetFromJsonAsync<ArenaRoomDto>($"api/v1/arena-rooms/{Uri.EscapeDataString(roomCode)}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<ArenaRoomDto?> SubmitArenaAnswerAsync(SubmitAnswerRequest req)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/v1/arena-rooms/answer", req);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<ArenaRoomDto>();
            }
        }
        catch { }
        return null;
    }

    public async Task LeaveArenaRoomAsync(string roomCode, string playerId)
    {
        try
        {
            await _http.PostAsync($"api/v1/arena-rooms/{Uri.EscapeDataString(roomCode)}/leave?playerId={Uri.EscapeDataString(playerId)}", null);
        }
        catch { }
    }

    public async Task<Stream?> GetFileStreamAsync(string url)
    {
        try
        {
            var res = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadAsStreamAsync();
            }
        }
        catch { }
        return null;
    }

    private class SimpleMessageDto
    {
        public string Message { get; set; } = string.Empty;
    }

    private class UploadResultDto
    {
        public Guid ContentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
    }

    private class AiQuizResultDto
    {
        public string Message { get; set; } = string.Empty;
        public int RemainingCoins { get; set; }
    }

    private class StarResultDto
    {
        public bool IsStarred { get; set; }
        public int StarCount { get; set; }
    }

    private class ContentQuizEnvelopeDto
    {
        public Guid ContentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public SubjectDto? Subject { get; set; }
        public QuizHolderDto? Quiz { get; set; }
    }

    private class SubjectDto
    {
        public int SubjectId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private class QuizHolderDto
    {
        public Guid ContentId { get; set; }
        public int TotalQuestions { get; set; }
        public int TimePerQuestionSec { get; set; } = 30;
        public List<QuizQuestionEntryDto> Questions { get; set; } = new();
    }

    private class QuizQuestionEntryDto
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = "SingleChoice";
        public string? Explanation { get; set; }
        public int Points { get; set; } = 10;
        public int OrderIndex { get; set; }
        public List<QuestionAnswerEntryDto> Answers { get; set; } = new();
    }

    private class QuestionAnswerEntryDto
    {
        public Guid AnswerId { get; set; }
        public string AnswerText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public int OrderIndex { get; set; }
    }
}
