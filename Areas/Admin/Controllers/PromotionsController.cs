using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public sealed class PromotionsController(THAN_NONG_SHOP_DbContext db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.Events = await db.PromotionVoucherEvents.AsNoTracking().OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync(cancellationToken);
        ViewBag.TodaySpins = await db.PromotionSpins.CountAsync(item => item.SpinDate == DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)), cancellationToken);
        return View(await db.PromotionRewards.OrderBy(item => item.Id).ToListAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string title, string description, string benefitMessage, decimal minimumSubtotal,
        decimal percentageDiscount, decimal fixedDiscount, decimal maximumDiscount, bool isFreeShipping, bool isGift,
        string? giftName, bool isPublicOffer, bool isActive, int wheelWeight, int? stockRemaining, CancellationToken cancellationToken)
    {
        var reward = await db.PromotionRewards.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (reward == null) return NotFound();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(benefitMessage) ||
            minimumSubtotal < 0 || percentageDiscount is < 0 or > 1 || fixedDiscount < 0 || maximumDiscount < 0 ||
            wheelWeight is < 0 or > 100000 || stockRemaining < 0)
        {
            TempData["PromotionAdminError"] = "Thông số khuyến mãi không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }
        reward.Title=title.Trim(); reward.Description=description.Trim(); reward.BenefitMessage=benefitMessage.Trim();
        reward.MinimumSubtotal=minimumSubtotal; reward.PercentageDiscount=percentageDiscount; reward.FixedDiscount=fixedDiscount;
        reward.MaximumDiscount=maximumDiscount; reward.IsFreeShipping=isFreeShipping; reward.IsGift=isGift;
        reward.GiftName=string.IsNullOrWhiteSpace(giftName)?null:giftName.Trim(); reward.IsPublicOffer=isPublicOffer;
        reward.IsActive=isActive; reward.WheelWeight=wheelWeight; reward.StockRemaining=stockRemaining;
        await db.SaveChangesAsync(cancellationToken);
        TempData["PromotionAdminSuccess"] = $"Đã cập nhật {reward.TemplateCode}.";
        return RedirectToAction(nameof(Index));
    }
}
