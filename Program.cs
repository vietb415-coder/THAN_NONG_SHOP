using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;

var builder = WebApplication.CreateBuilder(args);

// =========================
// 1. MVC
// =========================
builder.Services.AddControllersWithViews();


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
        options.Cookie.Name = "THAN_NONG_SHOP_Auth";
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
app.UseStaticFiles();

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