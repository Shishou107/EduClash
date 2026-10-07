using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api_Outline_management.Data;

namespace api_Outline_management.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SubjectsController : ControllerBase
{
    private readonly EduClashDbContext _context;

    public SubjectsController(EduClashDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetSubjects()
    {
        // Tự động khởi tạo đầy đủ các môn học Đại Học chuẩn nếu trong DB chưa có đủ
        try
        {
            var defaultSubjects = new List<Entities.Core.Subject>
            {
                new() { Code = "KTMT201", NormalizedCode = "KTMT201", Name = "Kiến trúc máy tính", Description = "Tập lệnh ISA, vi kiến trúc CPU, bộ nhớ Cache và pipeline", IsOfficial = true },
                new() { Code = "LLCT101", NormalizedCode = "LLCT101", Name = "Triết học Mác - Lênin", Description = "Thế giới quan và phương pháp luận triết học Mác - Lênin", IsOfficial = true },
                new() { Code = "PLDC101", NormalizedCode = "PLDC101", Name = "Pháp luật đại cương", Description = "Kiến thức cơ bản về nhà nước và hệ thống pháp luật Việt Nam", IsOfficial = true },
                new() { Code = "KTLT102", NormalizedCode = "KTLT102", Name = "Kỹ thuật lập trình", Description = "Con trỏ, cấp phát động, đệ quy, kỹ thuật tối ưu hóa mã nguồn", IsOfficial = true },
                new() { Code = "MATH202", NormalizedCode = "MATH202", Name = "Toán rời rạc", Description = "Logic mệnh đề, tập hợp, quan hệ, lý thuyết đồ thị và tổ hợp", IsOfficial = true },
                new() { Code = "NET201", NormalizedCode = "NET201", Name = "Mạng máy tính", Description = "Mô hình OSI, TCP/IP, địa chỉ IP, Subnet và các giao thức mạng", IsOfficial = true },
                new() { Code = "DB201", NormalizedCode = "DB201", Name = "Hệ Quản trị CSDL", Description = "Mô hình quan hệ ERD, chuẩn hóa 3NF, truy vấn SQL và ACID", IsOfficial = true },
                new() { Code = "CS201", NormalizedCode = "CS201", Name = "Cấu trúc dữ liệu & GT", Description = "Danh sách liên kết, ngăn xếp Stack, hàng đợi Queue, cây nhị phân", IsOfficial = true },
                new() { Code = "OS201", NormalizedCode = "OS201", Name = "Hệ điều hành", Description = "Quản lý tiến trình Process, luồng Thread, bộ nhớ ảo và Deadlock", IsOfficial = true },
                new() { Code = "ATTT301", NormalizedCode = "ATTT301", Name = "An toàn thông tin", Description = "Mã hóa đối xứng/bất đối xứng, chữ ký số, tường lửa và lỗ hổng mạng", IsOfficial = true },
                new() { Code = "MATH101", NormalizedCode = "MATH101", Name = "Toán cao cấp", Description = "Giải tích hàm một biến, ma trận định thức, đại số tuyến tính", IsOfficial = true },
                new() { Code = "LLCT102", NormalizedCode = "LLCT102", Name = "Tư tưởng Hồ Chí Minh", Description = "Hệ thống quan điểm toàn diện và sâu sắc về cách mạng Việt Nam", IsOfficial = true },
                new() { Code = "LLCT103", NormalizedCode = "LLCT103", Name = "Kinh tế chính trị Mác - Lênin", Description = "Học thuyết giá trị thặng dư, quy luật kinh tế thị trường và hội nhập", IsOfficial = true },
                new() { Code = "LLCT104", NormalizedCode = "LLCT104", Name = "Lịch sử Đảng Cộng sản VN", Description = "Sự ra đời của Đảng, quá trình lãnh đạo cách mạng và thời kỳ đổi mới", IsOfficial = true },
                new() { Code = "XHH101", NormalizedCode = "XHH101", Name = "Xã hội học", Description = "Các quy luật và hình thái phát triển xã hội", IsOfficial = true },
                new() { Code = "ENG101", NormalizedCode = "ENG101", Name = "Tiếng Anh chuyên ngành", Description = "Từ vựng học thuật, kỹ năng đọc hiểu tài liệu và dịch thuật kỹ thuật", IsOfficial = true },
                new() { Code = "CS101", NormalizedCode = "CS101", Name = "Nhập môn Lập trình", Description = "Biến, kiểu dữ liệu, cấu trúc điều khiển, mảng và hàm", IsOfficial = true }
            };

            bool changed = false;
            foreach (var ds in defaultSubjects)
            {
                if (!await _context.Subjects.AnyAsync(s => s.NormalizedCode == ds.NormalizedCode))
                {
                    _context.Subjects.Add(ds);
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }
        catch { /* Bỏ qua nếu DB đang bận */ }

        var subjects = await _context.Subjects
            .AsNoTracking()
            .Select(s => new
            {
                s.SubjectId,
                s.Code,
                s.Name,
                s.Description,
                s.IsOfficial,
                ContentsCount = s.Contents.Count(c => c.Visibility == "Public" && !c.IsDeleted)
            })
            .ToListAsync();

        return Ok(subjects);
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetPopularTags()
    {
        var tags = await _context.Tags
            .AsNoTracking()
            .OrderByDescending(t => t.UsageCount)
            .Take(30)
            .Select(t => new { t.TagId, t.TagName, t.UsageCount })
            .ToListAsync();

        return Ok(tags);
    }
}
