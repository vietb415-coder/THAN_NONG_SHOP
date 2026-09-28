using System.Security.Claims;
using THAN_NONG_SHOP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Controllers;

[Authorize(Roles = "User,Seller")]
public sealed class ProductReviewsController(THAN_NONG_SHOP_DbContext db,IWebHostEnvironment env) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(40_000_000), RequestFormLimits(MultipartBodyLengthLimit=40_000_000)]
    public async Task<IActionResult> Save(int productId, int rating, string comment, List<IFormFile>? media, CancellationToken ct)
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

        media ??= [];
        var types=media.Select(ReviewUploads.Inspect).ToList();
        if(types.Any(t=>t==null) || types.Count(t=>t!.Value.Video)>1 || types.Count(t=>!t!.Value.Video)>3){TempData["ReviewError"]="Tối đa 3 ảnh JPG/PNG/WebP (5 MB mỗi ảnh) và 1 video MP4 (20 MB). File phải đúng định dạng.";return RedirectToProduct(productId);}
        var review = await db.ProductReviews.Include(r=>r.Media).FirstOrDefaultAsync(item => item.ProductId == productId && item.UserName == userName, ct);
        if (review == null)
        {
            review = new ProductReview
            {
                ProductId = productId,
                UserName = userName,
                Rating = rating,
                Comment = comment,
                IsVerifiedPurchase = true
            };
            db.ProductReviews.Add(review);
        }
        else
        {
            review.Rating = rating;
            review.Comment = comment;
            review.IsVerifiedPurchase = true;
            review.UpdatedAt = DateTime.UtcNow;
        }

        var folder=Path.Combine(env.ContentRootPath,"App_Data","review-media");
        var written=new List<string>();var old=media.Count>0?review.Media.Select(m=>m.FileName).ToList():new List<string>();
        try {
            if(media.Count>0){Directory.CreateDirectory(folder);db.ReviewMedia.RemoveRange(review.Media);review.Media.Clear();}
            for(var i=0;i<media.Count;i++){
                var type=types[i]!.Value;var name=Guid.NewGuid().ToString("N")+type.Extension;
                written.Add(name);await using(var stream=System.IO.File.Create(Path.Combine(folder,name))){await media[i].CopyToAsync(stream,ct);}
                review.Media.Add(new ReviewMedia{FileName=name,ContentType=type.ContentType,IsVideo=type.Video});
            }
            await db.SaveChangesAsync(ct);
        }catch{foreach(var name in written)System.IO.File.Delete(Path.Combine(folder,name));throw;}
        foreach(var name in old)System.IO.File.Delete(Path.Combine(folder,Path.GetFileName(name)));
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
