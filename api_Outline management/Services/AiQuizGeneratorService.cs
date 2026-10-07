using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_Outline_management.DTOs;

namespace api_Outline_management.Services;

public interface IAiQuizGeneratorService
{
    Task<(bool success, string message, List<ParsedQuestionDto> questions)> GenerateQuizFromTheoryAsync(
        Guid userId, 
        string theoryText, 
        int coinCost = 3,
        int questionCount = 6);
}

public class AiQuizGeneratorService : IAiQuizGeneratorService
{
    private readonly ICoinService _coinService;

    public AiQuizGeneratorService(ICoinService coinService)
    {
        _coinService = coinService;
    }

    public async Task<(bool success, string message, List<ParsedQuestionDto> questions)> GenerateQuizFromTheoryAsync(
        Guid userId, 
        string theoryText, 
        int coinCost = 3,
        int questionCount = 6)
    {
        if (string.IsNullOrWhiteSpace(theoryText) || theoryText.Length < 20)
        {
            theoryText = string.IsNullOrWhiteSpace(theoryText) ? "Tổng quan kiến thức cốt lõi và các nguyên lý trọng tâm của đề cương học phần." : theoryText;
        }

        var cost = Math.Max(1, coinCost);
        var count = questionCount > 0 ? questionCount : cost * 2;

        // 1. Kiểm tra và khấu trừ Xu theo đúng số xu người dùng truyền vào (Quy chuẩn: 1 xu = 2 câu)
        var hasDeducted = await _coinService.DeductCoinsAsync(userId, cost, $"Dùng {cost} Xu tạo {count} câu hỏi trắc nghiệm AI (1 xu = 2 câu)");
        if (!hasDeducted)
        {
            var currentBalance = await _coinService.GetBalanceAsync(userId);
            return (false, $"Bạn không đủ Xu để sử dụng tính năng này (Cần {cost} Xu, hiện có {currentBalance} Xu).", new());
        }

        // 2. Phân tích nội dung và sinh câu hỏi trắc nghiệm
        var questions = GenerateQuestionsFromText(theoryText, count);
        return (true, $"Đã trừ {cost} Xu. AI đã sinh thành công {questions.Count} câu hỏi trắc nghiệm theo quy chuẩn 1 xu = 2 câu!", questions);
    }

    private static List<ParsedQuestionDto> GenerateQuestionsFromText(string text, int count)
    {
        var sentences = text.Split(new[] { '.', '\n', ';', '?' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => s.Length > 20 && s.Length < 250)
                            .Distinct()
                            .ToList();

        var questions = new List<ParsedQuestionDto>();
        var random = new Random();

        for (int i = 0; i < count; i++)
        {
            var sentence = (sentences.Count > 0)
                ? sentences[i % sentences.Count]
                : $"Khái niệm trọng tâm số {i + 1} trong đề cương lý thuyết";
            var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            string keyTerm = words.Length > 3 ? words[random.Next(1, Math.Min(4, words.Length))] : "khái niệm";

            var q = new ParsedQuestionDto
            {
                QuestionText = $"Theo tài liệu lý thuyết, phát biểu nào sau đây là ĐÚNG về '{sentence.Substring(0, Math.Min(60, sentence.Length))}...'?",
                Points = 10,
                Explanation = $"Dựa vào đoạn trích tài liệu: \"{sentence}\"",
                Answers = new List<ParsedAnswerDto>
                {
                    new() { AnswerText = sentence, IsCorrect = true, OrderIndex = 1 },
                    new() { AnswerText = $"Phát biểu ngược lại hoặc không được đề cập trong tài liệu về {keyTerm}.", IsCorrect = false, OrderIndex = 2 },
                    new() { AnswerText = $"Chỉ áp dụng trong một số trường hợp ngoại lệ đặc thù.", IsCorrect = false, OrderIndex = 3 },
                    new() { AnswerText = "Tất cả các khẳng định trên đều sai.", IsCorrect = false, OrderIndex = 4 }
                }
            };

            // Đảo ngẫu nhiên vị trí đáp án đúng
            int correctIndex = random.Next(0, 4);
            if (correctIndex != 0)
            {
                var temp = q.Answers[0];
                q.Answers[0] = q.Answers[correctIndex];
                q.Answers[correctIndex] = temp;
                for (int k = 0; k < q.Answers.Count; k++) q.Answers[k].OrderIndex = k + 1;
            }

            questions.Add(q);
        }

        return questions;
    }
}
