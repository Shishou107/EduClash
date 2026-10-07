using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using api_Outline_management.Data;
using api_Outline_management.Hubs;
using api_Outline_management.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình DbContext kết nối SQL Server với Chuẩn ACID (RCSI)
var connectionString = builder.Configuration.GetConnectionString("EduClashConnection") 
    ?? "Server=localhost;Database=EduClashDB;User Id=sa;Password=123456;TrustServerCertificate=True;MultipleActiveResultSets=True;";

builder.Services.AddDbContext<EduClashDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
        sqlOptions.MigrationsAssembly(typeof(EduClashDbContext).Assembly.FullName);
    });
});

// 2. Đăng ký Services & Business Logic
builder.Services.AddScoped<IEloService, EloService>();
builder.Services.AddScoped<IXpLevelService, XpLevelService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFileParserService, FileParserService>();
builder.Services.AddScoped<ICoinService, CoinService>();
builder.Services.AddScoped<IAiQuizGeneratorService, AiQuizGeneratorService>();

// 3. Cấu hình Authentication & JWT Bearer
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "EduClash_Super_Secret_Key_For_JWT_Authentication_2026_Minimum_32_Chars!";
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "EduClashAPI",
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"] ?? "EduClashClient",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Hỗ trợ truyền Token qua Query String cho SignalR WebSocket
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/gameplay"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// 4. SignalR cho Đấu trường Real-time (UC08)
builder.Services.AddSignalR();

// 5. Cấu hình Controllers & JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// 6. Cấu hình CORS cho Blazor UI (Hỗ trợ cả Localhost và mọi domain trên Somee)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowEduClashClient", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 7. Cấu hình Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Bật Swagger cho cả Development và Production (để xem được trên Somee)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EduClash API v1");
    c.RoutePrefix = string.Empty; // Mở Swagger ngay tại root URL: http://quamonba.somee.com/
});

// Hỗ trợ nếu người dùng gõ /swagger thì tự động chuyển về trang Swagger
app.MapGet("/swagger", () => Results.Redirect("/"));
app.MapGet("/swagger/index.html", () => Results.Redirect("/"));

app.UseCors("AllowEduClashClient");

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameplayHub>("/hubs/gameplay");

app.Run();
