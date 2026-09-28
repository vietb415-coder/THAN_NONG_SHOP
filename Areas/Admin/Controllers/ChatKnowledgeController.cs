using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public sealed class ChatKnowledgeController(THAN_NONG_SHOP_DbContext db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await db.ChatKnowledge.OrderBy(k => k.Category).ThenBy(k => k.Title).ToListAsync(ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ChatKnowledge item, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Index", await db.ChatKnowledge.OrderBy(k => k.Category).ToListAsync(ct));
        if (item.Id == 0) db.ChatKnowledge.Add(item);
        else
        {
            var current = await db.ChatKnowledge.FindAsync([item.Id], ct);
            if (current == null) return NotFound();
            current.Title = item.Title.Trim(); current.Content = item.Content.Trim(); current.Category = item.Category.Trim(); current.IsActive = item.IsActive; current.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        TempData["Success"] = "Đã lưu kiến thức cho chatbot.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await db.ChatKnowledge.FindAsync([id], ct);
        if (item != null) { db.ChatKnowledge.Remove(item); await db.SaveChangesAsync(ct); }
        return RedirectToAction(nameof(Index));
    }
}
