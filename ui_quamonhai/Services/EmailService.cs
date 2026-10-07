using System;
using System.Threading.Tasks;

namespace ui_quamonhai.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string displayName, string verifyLink);
    Task SendWelcomeEmailAsync(string toEmail, string displayName);
}

public class EmailService : IEmailService
{
    // STUB: Sẵn sàng cấu hình SMTP (Gmail, SendGrid...) khi cần
    public Task SendVerificationEmailAsync(string toEmail, string displayName, string verifyLink)
    {
        Console.WriteLine($"[EMAIL STUB] Gửi email xác thực tới: {toEmail} ({displayName}) | Link: {verifyLink}");
        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string toEmail, string displayName)
    {
        Console.WriteLine($"[EMAIL STUB] Gửi email chào mừng thành viên mới: {toEmail} ({displayName}) - Chào mừng bạn gia nhập EduClash!");
        return Task.CompletedTask;
    }
}
