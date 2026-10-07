using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace api_Outline_management.Controllers;

[ApiController]
[Route("api/v1/arena-rooms")]
public class ArenaRoomsController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, ArenaRoomDto> Rooms = new(StringComparer.OrdinalIgnoreCase);

    [HttpPost("create")]
    public IActionResult CreateRoom([FromBody] CreateRoomRequest req)
    {
        if (req == null || req.ContentId == Guid.Empty)
        {
            return BadRequest(new { Message = "Thông tin tạo phòng không hợp lệ. Cần chọn bộ đề trước!" });
        }

        // Tạo mã phòng 6 ký tự dễ nhớ, ví dụ: EDU-8924
        var rnd = new Random();
        string roomCode;
        int attempts = 0;
        do
        {
            roomCode = $"EDU-{rnd.Next(1000, 9999)}";
            attempts++;
        } while (Rooms.ContainsKey(roomCode) && attempts < 50);

        var room = new ArenaRoomDto
        {
            RoomCode = roomCode,
            ContentId = req.ContentId,
            QuizTitle = string.IsNullOrWhiteSpace(req.QuizTitle) ? "Đấu trường Tri thức" : req.QuizTitle,
            SubjectName = req.SubjectName ?? "Đại Cương",
            SubjectCode = req.SubjectCode ?? "GEN",
            CreatedAt = DateTime.UtcNow,
            Status = "Waiting",
            Seed = rnd.Next(1000, 999999),
            Host = new ArenaPlayerDto
            {
                PlayerId = string.IsNullOrWhiteSpace(req.PlayerId) ? Guid.NewGuid().ToString() : req.PlayerId,
                DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? "Chủ phòng" : req.DisplayName,
                AvatarUrl = req.AvatarUrl,
                Rating = req.Rating > 0 ? req.Rating : 1000,
                Level = req.Level > 0 ? req.Level : 1,
                RankTier = string.IsNullOrWhiteSpace(req.RankTier) ? "Đồng" : req.RankTier,
                Hp = 200,
                Score = 0
            }
        };

        Rooms[roomCode] = room;

        // Dọn dẹp phòng cũ quá 2 tiếng
        CleanStaleRooms();

        return Ok(room);
    }

    [HttpPost("join")]
    public IActionResult JoinRoom([FromBody] JoinRoomRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.RoomCode))
        {
            return BadRequest(new { Message = "Vui lòng nhập mã phòng đấu!" });
        }

        var normalizedCode = req.RoomCode.Trim().ToUpperInvariant();
        if (!Rooms.TryGetValue(normalizedCode, out var room))
        {
            return NotFound(new { Message = $"Không tìm thấy phòng với mã '{normalizedCode}'. Vui lòng kiểm tra lại mã!" });
        }

        if (room.Status != "Waiting")
        {
            return BadRequest(new { Message = "Phòng đấu này đã đủ người hoặc trận đấu đang diễn ra!" });
        }

        room.Guest = new ArenaPlayerDto
        {
            PlayerId = string.IsNullOrWhiteSpace(req.PlayerId) ? Guid.NewGuid().ToString() : req.PlayerId,
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? "Người thách đấu" : req.DisplayName,
            AvatarUrl = req.AvatarUrl,
            Rating = req.Rating > 0 ? req.Rating : 1000,
            Level = req.Level > 0 ? req.Level : 1,
            RankTier = string.IsNullOrWhiteSpace(req.RankTier) ? "Đồng" : req.RankTier,
            Hp = 200,
            Score = 0
        };

        room.Status = "Playing";
        return Ok(room);
    }

    [HttpGet("{roomCode}")]
    public IActionResult GetRoom(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode)) return BadRequest();
        var code = roomCode.Trim().ToUpperInvariant();

        if (Rooms.TryGetValue(code, out var room))
        {
            return Ok(room);
        }

        return NotFound(new { Message = "Phòng không tồn tại." });
    }

    [HttpPost("answer")]
    public IActionResult SubmitAnswer([FromBody] SubmitAnswerRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.RoomCode)) return BadRequest();
        var code = req.RoomCode.Trim().ToUpperInvariant();

        if (!Rooms.TryGetValue(code, out var room))
        {
            return NotFound(new { Message = "Phòng không tồn tại." });
        }

        bool isHost = room.Host.PlayerId.Equals(req.PlayerId, StringComparison.OrdinalIgnoreCase);
        bool isGuest = room.Guest != null && room.Guest.PlayerId.Equals(req.PlayerId, StringComparison.OrdinalIgnoreCase);

        if (!isHost && !isGuest)
        {
            return BadRequest(new { Message = "Người chơi không thuộc phòng này." });
        }

        var me = isHost ? room.Host : room.Guest!;
        var opponent = isHost ? room.Guest : room.Host;

        me.CurrentQuestionIndex = req.QuestionIndex;
        me.HasAnsweredCurrent = true;
        me.LastAnswerWasCorrect = req.IsCorrect;
        me.LastActionTime = DateTime.UtcNow;

        if (req.IsFinished)
        {
            me.IsFinished = true;
        }

        if (req.IsCorrect)
        {
            me.Score += 10;
            me.CorrectCount++;
            if (opponent != null)
            {
                opponent.Hp = Math.Max(0, opponent.Hp - 20); // Mỗi đòn đúng -20 HP
            }
        }

        // 1. Kiểm tra KNOCKOUT: Nếu 1 trong 2 người hết máu -> KẾT THÚC NGAY LẬP TỨC
        if (opponent != null && opponent.Hp <= 0)
        {
            room.Status = "Finished";
            room.WinnerPlayerId = me.PlayerId;
            room.WinReason = "Knockout";
        }
        else if (me.Hp <= 0 && opponent != null)
        {
            room.Status = "Finished";
            room.WinnerPlayerId = opponent.PlayerId;
            room.WinReason = "Knockout";
        }
        // 2. Kiểm tra CẢ HAI ĐÃ HOÀN THÀNH HẾT CÂU HỎI
        // NẾU 1 BÊN LÀM XONG TRƯỚC (20 CÂU) NHƯNG BÊN KIA CHƯA XONG -> VẪN ĐỂ STATUS = "Playing" ĐỂ ĐỢI!
        else if (me.IsFinished && (opponent == null || opponent.IsFinished))
        {
            room.Status = "Finished";
            room.WinReason = "HpComparison";
            if (opponent == null)
            {
                room.WinnerPlayerId = me.PlayerId;
            }
            else
            {
                // So sánh máu ai cao hơn
                if (me.Hp > opponent.Hp)
                {
                    room.WinnerPlayerId = me.PlayerId;
                }
                else if (opponent.Hp > me.Hp)
                {
                    room.WinnerPlayerId = opponent.PlayerId;
                }
                else
                {
                    // Bằng máu thì so sánh điểm số
                    if (me.Score > opponent.Score)
                    {
                        room.WinnerPlayerId = me.PlayerId;
                    }
                    else if (opponent.Score > me.Score)
                    {
                        room.WinnerPlayerId = opponent.PlayerId;
                    }
                    else
                    {
                        room.WinnerPlayerId = "DRAW"; // Hòa
                    }
                }
            }
        }

        return Ok(room);
    }

    [HttpPost("{roomCode}/leave")]
    public IActionResult LeaveRoom(string roomCode, [FromQuery] string? playerId = null, [FromBody] LeaveRoomRequest? body = null)
    {
        if (string.IsNullOrWhiteSpace(roomCode)) return BadRequest();
        var code = roomCode.Trim().ToUpperInvariant();
        var pId = !string.IsNullOrWhiteSpace(playerId) ? playerId : body?.PlayerId;

        if (Rooms.TryGetValue(code, out var room))
        {
            // Nếu phòng đang chờ và chủ phòng hủy -> xóa phòng
            if (room.Status == "Waiting" && (string.IsNullOrWhiteSpace(pId) || room.Host.PlayerId.Equals(pId, StringComparison.OrdinalIgnoreCase)))
            {
                Rooms.TryRemove(code, out _);
                return Ok(new { Message = "Đã hủy phòng đấu." });
            }

            // Nếu phòng đang đấu và có người thoát -> xử người còn lại WIN ngay lập tức
            room.Status = "Finished";
            room.AbandonedByPlayerId = pId ?? string.Empty;
            room.WinReason = "OpponentLeft";

            if (!string.IsNullOrWhiteSpace(pId) && room.Host.PlayerId.Equals(pId, StringComparison.OrdinalIgnoreCase))
            {
                room.WinnerPlayerId = room.Guest?.PlayerId;
            }
            else
            {
                room.WinnerPlayerId = room.Host.PlayerId;
            }
            return Ok(new { Message = "Đã rời phòng đấu. Đối thủ được xử thắng." });
        }

        return NotFound();
    }

    private static void CleanStaleRooms()
    {
        var cutoff = DateTime.UtcNow.AddHours(-2);
        foreach (var kvp in Rooms)
        {
            if (kvp.Value.CreatedAt < cutoff)
            {
                Rooms.TryRemove(kvp.Key, out _);
            }
        }
    }
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
    public string? WinReason { get; set; } // "Knockout" | "HpComparison" | "OpponentLeft" | "Draw"
    public int TotalQuestions { get; set; } = 20;
    public int Seed { get; set; } = 42;
}

public class LeaveRoomRequest
{
    public string PlayerId { get; set; } = string.Empty;
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
