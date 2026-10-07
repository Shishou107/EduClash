using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using ui_quamonhai.Models;

namespace ui_quamonhai.Services;

public interface IAuthService
{
    UserProfileViewModel? CurrentUser { get; }
    string? AccessToken { get; }
    Task<(bool success, string message)> RegisterAsync(RegisterRequest req);
    Task<(bool success, string message)> LoginAsync(LoginRequest req);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<UserProfileViewModel?> GetCurrentUserAsync();
    Task<(bool success, string message)> UpdateProfileAsync(UpdateProfileRequest req);
    Task<(bool success, string message)> ChangePasswordAsync(ChangePasswordRequest req);
    Task<(bool success, string message)> PurchaseCoinsAsync(string planId, int coins, int priceVnd);
}

public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEmailService _emailService;

    public AuthService(
        HttpClient http,
        IHttpContextAccessor httpContextAccessor,
        IEmailService emailService)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _emailService = emailService;
    }

    public string? AccessToken
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst("AccessToken")?.Value;
        }
    }

    public UserProfileViewModel? CurrentUser
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !(user.Identity?.IsAuthenticated ?? false)) return null;

            var idStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(idStr, out var userId);
            int.TryParse(user.FindFirst("Coins")?.Value, out var coins);
            int.TryParse(user.FindFirst("Elo")?.Value, out var elo);

            return new UserProfileViewModel
            {
                UserId = userId,
                Email = user.FindFirst(ClaimTypes.Email)?.Value ?? "",
                DisplayName = user.FindFirst(ClaimTypes.Name)?.Value ?? "",
                Role = user.FindFirst(ClaimTypes.Role)?.Value ?? "Member",
                AvatarUrl = user.FindFirst("AvatarUrl")?.Value,
                Coins = coins,
                Rating = elo,
                RankTier = user.FindFirst("RankTier")?.Value ?? "Đồng"
            };
        }
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        var isAuth = _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
        return Task.FromResult(isAuth);
    }

    public Task<UserProfileViewModel?> GetCurrentUserAsync()
    {
        return Task.FromResult(CurrentUser);
    }

    public async Task<(bool success, string message)> RegisterAsync(RegisterRequest req)
    {
        try
        {
            var payload = new
            {
                Email = req.Email.Trim(),
                Password = req.Password,
                DisplayName = req.DisplayName.Trim()
            };

            var res = await _http.PostAsJsonAsync("api/v1/auth/register", payload);
            if (res.IsSuccessStatusCode)
            {
                var verifyLink = "http://localhost:5163/verify-email";
                _ = _emailService.SendVerificationEmailAsync(req.Email, req.DisplayName, verifyLink);
                _ = _emailService.SendWelcomeEmailAsync(req.Email, req.DisplayName);
                return (true, "Đăng ký tài khoản thành công! Bạn được tặng sẵn 50 Xu. Vui lòng đăng nhập.");
            }
            else
            {
                var errorObj = await res.Content.ReadFromJsonAsync<ErrorResponseDto>();
                var msg = errorObj?.Message ?? "Đăng ký không thành công. Email có thể đã được sử dụng.";
                return (false, msg);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool success, string message)> LoginAsync(LoginRequest req)
    {
        try
        {
            var payload = new
            {
                Email = req.Email.Trim(),
                Password = req.Password
            };

            var res = await _http.PostAsJsonAsync("api/v1/auth/login", payload);
            if (res.IsSuccessStatusCode)
            {
                var authRes = await res.Content.ReadFromJsonAsync<BackendAuthResponseDto>();
                if (authRes?.User != null)
                {
                    var token = authRes.AccessToken ?? "";
                    var roles = authRes.User.Roles ?? new List<string>();
                    var role = roles.Contains("Admin") ? "Admin" : "Member";

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, authRes.User.UserId.ToString()),
                        new Claim(ClaimTypes.Name, authRes.User.DisplayName ?? "Người dùng"),
                        new Claim(ClaimTypes.Email, authRes.User.Email ?? ""),
                        new Claim(ClaimTypes.Role, role),
                        new Claim("Coins", authRes.User.Coins.ToString()),
                        new Claim("Elo", authRes.User.Rating.ToString()),
                        new Claim("RankTier", authRes.User.RankTier ?? "Đồng"),
                        new Claim("AvatarUrl", authRes.User.AvatarUrl ?? ""),
                        new Claim("AccessToken", token)
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    var httpCtx = _httpContextAccessor.HttpContext;
                    if (httpCtx != null)
                    {
                        await httpCtx.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            principal,
                            new AuthenticationProperties
                            {
                                IsPersistent = req.RememberMe,
                                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                            });
                    }

                    return (true, "Đăng nhập thành công!");
                }
            }

            var err = await res.Content.ReadFromJsonAsync<ErrorResponseDto>();
            return (false, err?.Message ?? "Email hoặc mật khẩu không chính xác.");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối máy chủ: {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        var httpCtx = _httpContextAccessor.HttpContext;
        if (httpCtx != null)
        {
            await httpCtx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    public async Task<(bool success, string message)> UpdateProfileAsync(UpdateProfileRequest req)
    {
        try
        {
            SetAuthHeader();
            var res = await _http.PutAsJsonAsync("api/v1/auth/me", req);
            if (res.IsSuccessStatusCode)
            {
                return (true, "Cập nhật hồ sơ thành công!");
            }
            return (false, "Không thể cập nhật hồ sơ.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string message)> ChangePasswordAsync(ChangePasswordRequest req)
    {
        try
        {
            SetAuthHeader();
            var res = await _http.PutAsJsonAsync("api/v1/auth/change-password", req);
            if (res.IsSuccessStatusCode)
            {
                return (true, "Đổi mật khẩu thành công!");
            }
            return (false, "Mật khẩu cũ không chính xác.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string message)> PurchaseCoinsAsync(string planId, int coins, int priceVnd)
    {
        try
        {
            SetAuthHeader();
            var res = await _http.PostAsJsonAsync("api/v1/wallet/purchase", new { PlanId = planId, Coins = coins, PriceVnd = priceVnd });
            if (res.IsSuccessStatusCode)
            {
                return (true, $"Nạp thành công +{coins} Xu!");
            }
            return (false, "Giao dịch không thành công.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private void SetAuthHeader()
    {
        var token = AccessToken;
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private class BackendAuthResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public BackendUserDto? User { get; set; }
    }

    private class BackendUserDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public int Coins { get; set; } = 50;
        public int Rating { get; set; } = 1000;
        public string RankTier { get; set; } = "Đồng";
        public List<string>? Roles { get; set; }
    }

    private class ErrorResponseDto
    {
        public string Message { get; set; } = string.Empty;
    }
}
