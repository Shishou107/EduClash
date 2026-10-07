using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ui_quamonhai.Models;
using ui_quamonhai.Services;

namespace ui_quamonhai.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest req, string? returnUrl)
    {
        if (!ModelState.IsValid)
        {
            return View(req);
        }

        var (success, message) = await _authService.LoginAsync(req);
        if (success)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        ViewBag.ErrorMessage = message;
        return View(req);
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (!ModelState.IsValid)
        {
            return View(req);
        }

        if (req.Password != req.ConfirmPassword)
        {
            ViewBag.ErrorMessage = "Mật khẩu xác nhận không khớp.";
            return View(req);
        }

        var (success, message) = await _authService.RegisterAsync(req);
        if (success)
        {
            TempData["SuccessMessage"] = message;
            return RedirectToAction("Login");
        }

        ViewBag.ErrorMessage = message;
        return View(req);
    }

    [HttpPost]
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public IActionResult Profile()
    {
        var user = _authService.CurrentUser;
        return View(user);
    }
}
