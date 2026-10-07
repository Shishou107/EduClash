using System;
using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using api_Outline_management.Data;
using api_Outline_management.Services;

namespace api_Outline_management.Hubs;

public record QueueEntry(string ConnectionId, Guid UserId, string DisplayName, int Rating, string SubjectCode, DateTimeOffset EnqueuedAt);

[Authorize]
public class GameplayHub : Hub
{
    private static readonly ConcurrentDictionary<string, QueueEntry> MatchQueue = new();
    private static readonly ConcurrentDictionary<string, Guid> ConnectionUserMap = new();

    private readonly EduClashDbContext _context;
    private readonly IEloService _eloService;
    private readonly IXpLevelService _xpLevelService;

    public GameplayHub(EduClashDbContext context, IEloService eloService, IXpLevelService xpLevelService)
    {
        _context = context;
        _eloService = eloService;
        _xpLevelService = xpLevelService;
    }

    public override async Task OnConnectedAsync()
    {
        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdStr, out var userId))
        {
            ConnectionUserMap[Context.ConnectionId] = userId;
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        MatchQueue.TryRemove(Context.ConnectionId, out _);
        ConnectionUserMap.TryRemove(Context.ConnectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Player tham gia hàng đợi tìm trận theo mã môn học (UC08)
    /// </summary>
    public async Task JoinMatchmakingQueue(string subjectCode)
    {
        if (!ConnectionUserMap.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("OnError", "Chưa xác thực danh tính.");
            return;
        }

        var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            await Clients.Caller.SendAsync("OnError", "Không tìm thấy hồ sơ đấu thủ.");
            return;
        }

        var currentEntry = new QueueEntry(Context.ConnectionId, userId, profile.DisplayName, profile.Rating, subjectCode, DateTimeOffset.UtcNow);

        // Tìm đối thủ trong hàng đợi có môn học tương ứng và chênh lệch Rating <= 200
        var opponent = MatchQueue.Values
            .FirstOrDefault(q => q.UserId != userId 
                             && q.SubjectCode.Equals(subjectCode, StringComparison.OrdinalIgnoreCase)
                             && Math.Abs(q.Rating - profile.Rating) <= 200);

        if (opponent != null)
        {
            // Ghép cặp thành công -> Xóa cả 2 khỏi queue
            MatchQueue.TryRemove(opponent.ConnectionId, out _);
            MatchQueue.TryRemove(Context.ConnectionId, out _);

            // Tìm một bộ Quiz thuộc môn học này
            var quiz = await _context.Quizzes
                .Include(q => q.Content)
                .Include(q => q.Questions)
                .ThenInclude(qu => qu.Answers)
                .FirstOrDefaultAsync(q => q.IsPvPEnabled 
                                       && q.Content.Subject.Code == subjectCode 
                                       && q.Content.Visibility == "Public" 
                                       && !q.Content.IsDeleted);

            var matchId = Guid.NewGuid();
            var groupName = $"match_{matchId}";

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await Groups.AddToGroupAsync(opponent.ConnectionId, groupName);

            var matchDataForPlayer1 = new
            {
                MatchId = matchId,
                OpponentName = opponent.DisplayName,
                OpponentRating = opponent.Rating,
                QuizTitle = quiz?.Content.Title ?? "Đấu trường Tri Thức",
                TimePerQuestion = quiz?.TimePerQuestionSec ?? 30,
                TotalQuestions = quiz?.Questions.Count ?? 5
            };

            var matchDataForPlayer2 = new
            {
                MatchId = matchId,
                OpponentName = profile.DisplayName,
                OpponentRating = profile.Rating,
                QuizTitle = quiz?.Content.Title ?? "Đấu trường Tri Thức",
                TimePerQuestion = quiz?.TimePerQuestionSec ?? 30,
                TotalQuestions = quiz?.Questions.Count ?? 5
            };

            await Clients.Client(Context.ConnectionId).SendAsync("OnMatchFound", matchDataForPlayer1);
            await Clients.Client(opponent.ConnectionId).SendAsync("OnMatchFound", matchDataForPlayer2);
        }
        else
        {
            // Đưa vào hàng đợi chờ ghép
            MatchQueue[Context.ConnectionId] = currentEntry;
            await Clients.Caller.SendAsync("OnQueueEnqueued", new { Status = "Đang tìm đối thủ...", SubjectCode = subjectCode });
        }
    }

    /// <summary>
    /// Rời hàng đợi tìm trận
    /// </summary>
    public async Task CancelMatchmaking()
    {
        MatchQueue.TryRemove(Context.ConnectionId, out _);
        await Clients.Caller.SendAsync("OnQueueCancelled", "Đã hủy tìm trận.");
    }

    /// <summary>
    /// Gửi tiến độ câu hỏi để đối phương nhìn thấy thanh tiến trình real-time
    /// </summary>
    public async Task NotifyQuestionProgress(string matchId, int questionIndex, int currentScore)
    {
        var groupName = $"match_{matchId}";
        await Clients.OthersInGroup(groupName).SendAsync("OnOpponentProgress", new
        {
            CurrentQuestionIndex = questionIndex,
            OpponentScore = currentScore
        });
    }
}
