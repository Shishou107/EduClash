using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using api_Outline_management.DTOs;

namespace api_Outline_management.Services;

public interface IFileParserService
{
    Task<string> ExtractTextAsync(Stream fileStream, string fileName);
    List<ParsedQuestionDto> ParseStructuredQuiz(string rawText);
    List<OutlineSectionDto> ExtractCollapsibleSections(string rawText);
}

public class FileParserService : IFileParserService
{
    public async Task<string> ExtractTextAsync(Stream fileStream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".docx" => await ExtractTextFromDocxAsync(fileStream),
            ".pdf" => await ExtractTextFromPdfAsync(fileStream),
            _ => await ExtractTextFromPlainTextAsync(fileStream)
        };
    }

    private static Task<string> ExtractTextFromDocxAsync(Stream stream)
    {
        return Task.Run(() =>
        {
            try
            {
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                using var wordDoc = WordprocessingDocument.Open(memoryStream, false);
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                if (body == null) return string.Empty;

                var sb = new StringBuilder();
                foreach (var element in body.Elements())
                {
                    if (element is Paragraph p)
                    {
                        var text = ExtractParagraphWithStyles(p);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            sb.AppendLine(text.Trim());
                        }
                    }
                    else if (element is Table tbl)
                    {
                        foreach (var row in tbl.Elements<TableRow>())
                        {
                            foreach (var cell in row.Elements<TableCell>())
                            {
                                foreach (var cp in cell.Elements<Paragraph>())
                                {
                                    var text = ExtractParagraphWithStyles(cp);
                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        sb.AppendLine(text.Trim());
                                    }
                                }
                            }
                        }
                    }
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"[Lỗi đọc DOCX: {ex.Message}]";
            }
        });
    }

    private static string ExtractRunText(Run run)
    {
        var sb = new StringBuilder();
        foreach (var child in run.ChildElements)
        {
            if (child is Text t)
            {
                sb.Append(t.Text);
            }
            else if (child is TabChar)
            {
                sb.Append("\t");
            }
            else if (child is Break)
            {
                sb.Append("\n");
            }
        }
        return sb.ToString();
    }

    private static string ExtractParagraphWithStyles(Paragraph param)
    {
        var runs = param.Descendants<Run>().ToList();
        if (runs.Count == 0)
        {
            return param.InnerText.Trim();
        }

        var sb = new StringBuilder();
        foreach (var run in runs)
        {
            var text = ExtractRunText(run);
            if (string.IsNullOrEmpty(text)) continue;

            var isBold = run.RunProperties?.Bold != null && 
                         (run.RunProperties.Bold.Val == null || run.RunProperties.Bold.Val.Value);
            
            if (isBold)
            {
                sb.Append("**").Append(text).Append("**");
            }
            else
            {
                sb.Append(text);
            }
        }
        return sb.ToString().Trim();
    }

    private static Task<string> ExtractTextFromPdfAsync(Stream stream)
    {
        return Task.Run(() =>
        {
            try
            {
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                using var pdf = PdfDocument.Open(memoryStream);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    var words = page.GetWords().ToList();
                    if (words.Count > 0)
                    {
                        // Nhóm các từ có cùng tọa độ trục Y thành từng dòng riêng biệt
                        var lineGroups = words
                            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 5.0) * 5.0)
                            .OrderByDescending(g => g.Key);

                        foreach (var group in lineGroups)
                        {
                            var sortedWords = group.OrderBy(w => w.BoundingBox.Left);
                            var sbLine = new StringBuilder();
                            foreach (var word in sortedWords)
                            {
                                var isBold = word.Letters.Any(l => l.FontName != null && 
                                    (l.FontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) || 
                                     l.FontName.Contains("Black", StringComparison.OrdinalIgnoreCase) ||
                                     l.FontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase)));
                                if (isBold)
                                {
                                    sbLine.Append("**").Append(word.Text).Append("** ");
                                }
                                else
                                {
                                    sbLine.Append(word.Text).Append(" ");
                                }
                            }
                            var lStr = sbLine.ToString().Trim();
                            if (!string.IsNullOrWhiteSpace(lStr))
                            {
                                sb.AppendLine(lStr);
                            }
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(page.Text))
                    {
                        sb.AppendLine(page.Text.Trim());
                    }
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"[Lỗi đọc PDF: {ex.Message}]";
            }
        });
    }

    private static async Task<string> ExtractTextFromPlainTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Bóc tách đề trắc nghiệm có sẵn: Câu 1 ... A. ... B. ... C. ... D. ...
    /// Hỗ trợ in đậm (chữ in đậm là đáp án đúng) và một câu có thể có nhiều đáp án đúng.
    /// </summary>
    public List<ParsedQuestionDto> ParseStructuredQuiz(string rawText)
    {
        var questions = new List<ParsedQuestionDto>();
        if (string.IsNullOrWhiteSpace(rawText)) return questions;

        var rawLines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();

        // Regex thông minh bóc tách các phương án A, B, C, D nằm trên cùng một dòng (do bảng hoặc tab/khoảng trắng)
        var multiOptionSplitRegex = new Regex(
            @"(?<=\S)(?:[\t\s]{2,}|\t|\s+(?=(?:\*{1,2}|<b>|<strong>|\[x\]|\[X\])?\s*[\(\[]?[B-F][\)\]\.\:\-]\s+))(?=(?:\*{1,2}|<b>|<strong>|\[x\]|\[X\])?\s*[\(\[]?[A-F][\)\]\.\:\-]\s+)",
            RegexOptions.IgnoreCase);

        foreach (var rLine in rawLines)
        {
            var trimmed = rLine.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var parts = multiOptionSplitRegex.Split(trimmed);
            foreach (var part in parts)
            {
                var pt = part.Trim();
                if (!string.IsNullOrEmpty(pt))
                {
                    lines.Add(pt);
                }
            }
        }

        ParsedQuestionDto? currentQuestion = null;
        var questionRegex = new Regex(
            @"^\s*(?:[\*\b_]*)\s*(?:\[?(?:Câu|Question|Bài)\s*(\d+)\]?|\b(\d+)[\.\)\/\-])\s*[\:\.\-]?\s*(?:[\*\b_]*)\s*(.*)",
            RegexOptions.IgnoreCase);
        // Hỗ trợ phương án A, B, C, D, E, F có hoặc không có dấu in đậm/ngoặc vuông/ngoặc tròn
        var answerRegex = new Regex(
            @"^\s*(?:[\*\b_]*)\s*[\(\[]?\s*([A-F])\s*(?:[\*\b_]*)\s*[\.\)\:\-]\s*(?:[\*\b_]*)\s*(.*)",
            RegexOptions.IgnoreCase);
        var answerKeyRegex = new Regex(@"(?:Đáp án|Key|Ans)[\s:.]*([A-F](?:\s*[,;&và\s]\s*[A-F])*)", RegexOptions.IgnoreCase);

        void FinalizeQuestion(ParsedQuestionDto q)
        {
            if (q.Answers.Count < 2) return;

            var correctCount = q.Answers.Count(a => a.IsCorrect);
            if (correctCount == 0)
            {
                // Mặc định nếu không đánh dấu đáp án thì chọn phương án đầu tiên
                q.Answers[0].IsCorrect = true;
                correctCount = 1;
            }

            // Hỗ trợ một câu có nhiều đáp án đúng
            q.QuestionType = correctCount > 1 ? "MultipleChoice" : "SingleChoice";
            questions.Add(q);
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var qMatch = questionRegex.Match(trimmed);
            if (qMatch.Success)
            {
                if (currentQuestion != null)
                {
                    FinalizeQuestion(currentQuestion);
                }

                var rawQText = qMatch.Groups[3].Value.Trim();
                var cleanQText = Regex.Replace(rawQText, @"^[\*_\s:]+|[\*_\s]+$", "").Trim();
                currentQuestion = new ParsedQuestionDto
                {
                    QuestionText = string.IsNullOrEmpty(cleanQText) ? trimmed : cleanQText,
                    Points = 10
                };
                continue;
            }

            var aMatch = answerRegex.Match(trimmed);
            if (aMatch.Success && currentQuestion != null)
            {
                var optChar = aMatch.Groups[1].Value.ToUpperInvariant();
                var rawAnswer = aMatch.Groups[2].Value.Trim();

                // Kiểm tra xem phương án này (phần nội dung phương án) có in đậm hoặc được đánh dấu là đáp án đúng không
                var isBold = rawAnswer.Contains("**") || 
                             rawAnswer.Contains("<b>") || 
                             rawAnswer.Contains("<strong>") || 
                             rawAnswer.Contains("__");

                var isExplicitMark = trimmed.Contains("[x]") || 
                                     trimmed.Contains("[X]") || 
                                     trimmed.Contains("(đúng)") || 
                                     trimmed.Contains("(dung)") || 
                                     trimmed.Contains("(*) ") || 
                                     trimmed.EndsWith("(*)");

                var isMarkedCorrect = isBold || isExplicitMark;

                // Làm sạch văn bản đáp án để hiển thị giao diện đẹp mắt (bỏ các ký hiệu in đậm và đánh dấu)
                var cleanAnswer = Regex.Replace(rawAnswer, @"\*\*|<b>|<\/b>|<strong>|<\/strong>|__", "");
                cleanAnswer = Regex.Replace(cleanAnswer, @"\[x\]|\[X\]|\(đúng\)|\(dung\)|\(\*\)", "", RegexOptions.IgnoreCase);
                cleanAnswer = cleanAnswer.Trim().TrimEnd('*').Trim();

                currentQuestion.Answers.Add(new ParsedAnswerDto
                {
                    AnswerText = string.IsNullOrEmpty(cleanAnswer) ? rawAnswer : cleanAnswer,
                    IsCorrect = isMarkedCorrect,
                    OrderIndex = currentQuestion.Answers.Count + 1
                });
                continue;
            }

            // Kiểm tra dòng đáp án cuối câu (ví dụ: "Đáp án: A, C" hoặc "Key: B")
            if (currentQuestion != null)
            {
                var keyMatch = answerKeyRegex.Match(trimmed);
                if (keyMatch.Success)
                {
                    var keysStr = keyMatch.Groups[1].Value.ToUpperInvariant();
                    foreach (var ch in new[] { 'A', 'B', 'C', 'D', 'E', 'F' })
                    {
                        if (keysStr.Contains(ch))
                        {
                            int targetIdx = ch - 'A';
                            if (targetIdx >= 0 && targetIdx < currentQuestion.Answers.Count)
                            {
                                currentQuestion.Answers[targetIdx].IsCorrect = true;
                            }
                        }
                    }
                    continue;
                }

                // Nếu là dòng text tiếp theo của câu hỏi (chưa có phương án nào)
                if (currentQuestion.Answers.Count == 0)
                {
                    currentQuestion.QuestionText += " " + trimmed;
                }
                // Nếu đã có phương án, nối tiếp vào phương án cuối cùng (tránh rớt chữ khi đáp án dài nhiều dòng)
                else if (currentQuestion.Answers.Count > 0)
                {
                    var lastAns = currentQuestion.Answers.Last();
                    var cleanCont = Regex.Replace(trimmed, @"\*\*|<b>|<\/b>|<strong>|<\/strong>|__", "").Trim();
                    lastAns.AnswerText += " " + cleanCont;
                    if (trimmed.Contains("**") || trimmed.Contains("<b>") || trimmed.Contains("<strong>"))
                    {
                        lastAns.IsCorrect = true;
                    }
                }
            }
        }

        if (currentQuestion != null)
        {
            FinalizeQuestion(currentQuestion);
        }

        return questions;
    }

    /// <summary>
    /// Phân tích tài liệu đề cương lý thuyết thành các mục sổ xuống (Collapsible Sections)
    /// </summary>
    public List<OutlineSectionDto> ExtractCollapsibleSections(string rawText)
    {
        var sections = new List<OutlineSectionDto>();
        if (string.IsNullOrWhiteSpace(rawText)) return sections;

        var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sectionHeaderRegex = new Regex(@"^(Chương|Phần|Bài|Mục|Phần\s+\d+|Chương\s+\d+|[IVXLCDM]+\.|\d+\.)\s*(.*)", RegexOptions.IgnoreCase);

        OutlineSectionDto? currentSection = null;
        var contentSb = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var match = sectionHeaderRegex.Match(trimmed);

            if (match.Success && trimmed.Length < 100)
            {
                if (currentSection != null)
                {
                    currentSection.Content = contentSb.ToString().Trim();
                    sections.Add(currentSection);
                    contentSb.Clear();
                }

                currentSection = new OutlineSectionDto
                {
                    SectionTitle = trimmed,
                    OrderIndex = sections.Count + 1
                };
            }
            else
            {
                if (currentSection == null)
                {
                    currentSection = new OutlineSectionDto
                    {
                        SectionTitle = "Tổng Quan & Mở Đầu",
                        OrderIndex = 1
                    };
                }
                contentSb.AppendLine(line);
            }
        }

        if (currentSection != null)
        {
            currentSection.Content = contentSb.ToString().Trim();
            sections.Add(currentSection);
        }

        if (sections.Count == 0)
        {
            sections.Add(new OutlineSectionDto
            {
                SectionTitle = "Toàn Bộ Nội Dung Đề Cương",
                Content = rawText,
                OrderIndex = 1
            });
        }

        return sections;
    }
}
