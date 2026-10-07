namespace ui_quamonhai.Models;

// Hàm nhỏ dùng chung trong các view.
public static class UiHelpers
{
    public static string TierClass(string? tier)
    {
        var t = (tier ?? "").Trim().ToLowerInvariant();
        if (t.Contains("kim cương") || t.Contains("diamond")) return "tier--kimcuong";
        if (t.Contains("bạch kim") || t.Contains("platinum")) return "tier--bachkim";
        if (t.Contains("vàng") || t.Contains("gold")) return "tier--vang";
        if (t.Contains("bạc") || t.Contains("silver")) return "tier--bac";
        return "";
    }

    public static string Initial(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();
}
