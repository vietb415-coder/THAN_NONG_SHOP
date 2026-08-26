using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Identity;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Data;
using System.Security.Claims;
using THAN_NONG_SHOP.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// =========================
// 1. MVC
// =========================
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IPasswordHasher<user>, PasswordHasher<user>>();
builder.Services.AddMemoryCache();
builder.Services.Configure<ChatbotOptions>(builder.Configuration.GetSection("Chatbot"));
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddRateLimiter(options => options.AddPolicy("chat", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 2,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        })));
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});


// =========================
// 2. DATABASE
// =========================
var connectionString = builder.Configuration
    .GetConnectionString("THAN_NONG_SHOP_ConnectionString");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Không tìm thấy ConnectionString: THAN_NONG_SHOP_ConnectionString");
}

builder.Services.AddDbContext<THAN_NONG_SHOP_DbContext>(options =>
{
    options.UseSqlServer(connectionString);
});


// =========================
// 3. HTTP CONTEXT
// =========================
builder.Services.AddHttpContextAccessor();


// =========================
// 4. SESSION
// =========================

// Bắt buộc có cache để Session hoạt động
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    options.Cookie.Name = "THAN_NONG_SHOP_Session";
});


// =========================
// 5. AUTHENTICATION
// =========================
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";

        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        // Đổi phiên bản cookie khi cấu trúc claims thay đổi để không dùng lại tên hiển thị cũ.
        options.Cookie.Name = "THAN_NONG_SHOP_Auth_v2";
        options.Events.OnValidatePrincipal = async context =>
        {
            var username = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(username))
            {
                context.RejectPrincipal();
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<THAN_NONG_SHOP_DbContext>();
            var account = await db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.UserName == username);
            var expectedRole = account?.RoleId == 1 ? "Admin" : "User";
            var currentRole = context.Principal?.FindFirstValue(ClaimTypes.Role);
            if (account == null || !account.IsActive || currentRole != expectedRole)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });


// =========================
// BUILD APP
// =========================
var app = builder.Build();


// =========================
// 6. ERROR HANDLING
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


// =========================
// 7. MIDDLEWARE
// =========================
app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "public,max-age=604800";
    }
});

// Static Assets của .NET 10
app.MapStaticAssets();

app.UseRouting();
app.UseRateLimiter();

// Xác thực
app.UseAuthentication();

// Phân quyền
app.UseAuthorization();

// Session
app.UseSession();


// =========================
// 8. ROUTING ADMIN
// =========================
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


// =========================
// 9. ROUTING CLIENT
// =========================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


// =========================
// 10. SEED DATABASE
// =========================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context =
            services.GetRequiredService<THAN_NONG_SHOP_DbContext>();

        context.Database.Migrate();
        DbInitializer.Seed(context);
        ChatKnowledgeSeeder.Seed(context);

        var logger =
            services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Migration và Seed Data thành công.");
    }
    catch (Exception ex)
    {
        var logger =
            services.GetRequiredService<ILogger<Program>>();

        logger.LogError(
            ex,
            "Không thể kết nối, migration hoặc seed cơ sở dữ liệu. Hãy kiểm tra SQL Server và ConnectionString.");
        throw;
    }
}


// =========================
// RUN
// =========================
app.Run();
