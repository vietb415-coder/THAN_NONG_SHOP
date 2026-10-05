using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Services;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EmailController(THAN_NONG_SHOP_DbContext db, IConfiguration config,
    IWebHostEnvironment env, EmailWorkerStatus status) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var pending = await db.EmailMessages.CountAsync(m => m.SentAt == null, ct);
        var retrying = await db.EmailMessages.CountAsync(m => m.SentAt == null && m.Attempts > 0, ct);
        var sent = await db.EmailMessages.CountAsync(m => m.SentAt != null, ct);
        return View(new EmailDiagnosticsModel(env.EnvironmentName, config["Email:SmtpHost"] ?? "",
            config["Email:From"] ?? "", EmailConfiguration.Errors(config, env), pending, retrying, sent, status.Current));
    }
}
