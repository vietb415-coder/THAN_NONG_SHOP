using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Identity;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Data;

var builder = WebApplication.CreateBuilder(args);

// =========================
// 1. MVC
// =========================
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IPasswordHasher<user>, PasswordHasher<user>>();
builder.Services.AddMemoryCache();
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

        DbInitializer.Seed(context);

        var logger =
            services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Seed Data thành công.");
    }
    catch (Exception ex)
    {
        var logger =
            services.GetRequiredService<ILogger<Program>>();

        logger.LogError(
            ex,
            "Lỗi xảy ra trong quá trình Seed Data.");
    }
}


// =========================
// RUN
// =========================
app.Run();
