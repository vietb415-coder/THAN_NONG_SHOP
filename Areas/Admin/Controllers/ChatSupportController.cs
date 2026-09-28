using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public sealed class ChatSupportController(THAN_NONG_SHOP_DbContext db) : Controller
{
    public async Task<IActionResult> Index(Guid? id, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var model = new ChatAdminDashboard
        {
            ConversationsToday = await db.ChatConversations.CountAsync(c => c.StartedAt >= today, ct),
            NeedsHuman = await db.ChatConversations.CountAsync(c => c.NeedsHuman, ct),
            Helpful = await db.ChatMessages.CountAsync(m => m.Helpful == true, ct),
            NotHelpful = await db.ChatMessages.CountAsync(m => m.Helpful == false, ct),
            Conversations = await db.ChatConversations.AsNoTracking().OrderByDescending(c => c.NeedsHuman).ThenByDescending(c => c.LastMessageAt).Take(100).ToListAsync(ct)
        };
        if (id.HasValue) model.Selected = await db.ChatConversations.AsNoTracking().Include(c => c.Messages.OrderBy(m => m.CreatedAt)).FirstOrDefaultAsync(c => c.Id == id, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        var conversation = await db.ChatConversations.FindAsync([id], ct);
        if (conversation != null) { conversation.NeedsHuman = false; await db.SaveChangesAsync(ct); }
        return RedirectToAction(nameof(Index), new { id });
    }
}
