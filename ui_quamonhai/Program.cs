using Microsoft.AspNetCore.Authentication.Cookies;
using ui_quamonhai.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MVC Controllers & Views
builder.Services.AddControllersWithViews();

// 2. HTTP Client connecting to Somee Backend API
var backendUrl = builder.Configuration["BackendApiUrl"] ?? "http://quamonba.somee.com/";
if (!backendUrl.EndsWith("/")) backendUrl += "/";

builder.Services.AddHttpClient("EduClashApi", client =>
{
    client.BaseAddress = new Uri(backendUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("EduClashApi"));
builder.Services.AddScoped<IEduClashApiClient, EduClashApiClient>();

// 3. Authentication & Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
