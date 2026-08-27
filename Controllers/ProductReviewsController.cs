using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Controllers;

[Authorize(Roles = "User")]
public sealed class ProductReviewsController(THAN_NONG_SHOP_DbContext db) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int productId, int rating, string comment, CancellationToken ct)
    {
        var userName = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userName)) return Challenge();
        if (!await db.Products.AnyAsync(product => product.Id == productId, ct)) return NotFound();

        comment = comment?.Trim() ?? string.Empty;
        if (rating is < 1 or > 5 || comment.Length is < 3 or > 1000)
        {
            TempData["ReviewError"] = "Đánh giá phải có 1–5 sao và nội dung từ 3 đến 1000 ký tự.";
            return RedirectToProduct(productId);
        }

        var purchased = await db.OderDetails.AsNoTracking().AnyAsync(detail =>
            detail.ProductId == productId && detail.Oder != null &&
            detail.Oder.UserName == userName && detail.Oder.Status == OrderStatus.Completed, ct);
        if (!purchased)
        {
            TempData["ReviewError"] = "Bạn chỉ có thể đánh giá sản phẩm trong đơn đã hoàn thành.";
            return RedirectToProduct(productId);
        }

        var review = await db.ProductReviews.FirstOrDefaultAsync(item => item.ProductId == productId && item.UserName == userName, ct);
        if (review == null)
        {
            db.ProductReviews.Add(new ProductReview
            {
                ProductId = productId,
                UserName = userName,
                Rating = rating,
                Comment = comment,
                IsVerifiedPurchase = true
            });
        }
        else
        {
            review.Rating = rating;
            review.Comment = comment;
            review.IsVerifiedPurchase = true;
            review.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        TempData["ReviewSuccess"] = "Cảm ơn bạn đã đánh giá sản phẩm.";
        return RedirectToProduct(productId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int productId, CancellationToken ct)
    {
        var userName = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var review = await db.ProductReviews.FirstOrDefaultAsync(item => item.ProductId == productId && item.UserName == userName, ct);
        if (review != null)
        {
            db.ProductReviews.Remove(review);
            await db.SaveChangesAsync(ct);
        }
        return RedirectToProduct(productId);
    }

    private RedirectToActionResult RedirectToProduct(int productId) =>
        RedirectToAction("Details", "Products", new { id = productId });
}
