using System.Globalization;
using System.Text;

namespace api_Outline_management.Services;

public static class TagHelper
{
    /// <summary>
    /// Chuẩn hóa chuỗi bằng cách xóa dấu tiếng Việt, khoảng trắng và ký tự đặc biệt.
    /// Ví dụ: "Toán cao cấp" -> "toancaocap", "Chương rời rạc" -> "chuongroirac"
    /// </summary>
    public static string RemoveDiacriticsAndSpaces(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                if (c == 'đ' || c == 'Đ') sb.Append('d');
                else if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Chuyển đổi tên tag người dùng nhập thành định dạng hashtag chuẩn.
    /// Ví dụ: "toán cao cấp" -> "#toancaocap"
    /// </summary>
    public static string ToHashtag(string text)
    {
        var clean = RemoveDiacriticsAndSpaces(text);
        return string.IsNullOrEmpty(clean) ? string.Empty : "#" + clean;
    }
}
