using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Services;
namespace THAN_NONG_SHOP.Areas.Admin.Controllers;
[Area("Admin"),Authorize(Roles="Admin,Seller")]
public sealed class AdminOrdersController(THAN_NONG_SHOP_DbContext db,OrderLifecycle lifecycle):Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if(User.IsInRole("Seller")) return View("Seller",await db.OderDetails.AsNoTracking().Include(d=>d.Oder).Include(d=>d.Product).Where(d=>d.SellerUserName==User.FindFirstValue(ClaimTypes.NameIdentifier)).OrderByDescending(d=>d.OderId).ToListAsync(ct));
        var orders=await db.Oders.AsNoTracking().OrderByDescending(o=>o.OrderDate).ToListAsync(ct);
        ViewBag.OrderPromotions=await db.PromotionVouchers.Where(v=>v.OrderId!=null).ToDictionaryAsync(v=>v.OrderId!.Value,v=>v.TemplateCode,ct);
        ViewBag.OrderGifts=await db.OrderGiftItems.GroupBy(i=>i.OrderId).ToDictionaryAsync(g=>g.Key,g=>string.Join(", ",g.Select(i=>i.Name)),ct);
        return View(orders);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int orderId,string newStatus,int? detailId,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var o=await db.Oders.FirstOrDefaultAsync(o=>o.Id==orderId,ct);if(o==null)return NotFound();
        var lines=await db.OderDetails.Where(d=>d.OderId==o.Id).ToListAsync(ct);
        if(User.IsInRole("Seller")) {
            var line=lines.FirstOrDefault(d=>d.Id==detailId && d.SellerUserName==User.FindFirstValue(ClaimTypes.NameIdentifier));if(line==null)return NotFound();
            if(o.Status is OrderStatus.AwaitingPayment or OrderStatus.Cancelled || newStatus==OrderStatus.Cancelled || !OrderStatus.CanMove(line.FulfillmentStatus,newStatus))return BadRequest("Không thể chuyển trạng thái này. Đơn phải được thanh toán trước khi xử lý.");
            line.FulfillmentStatus=newStatus;
            if(lines.All(d=>d.FulfillmentStatus==OrderStatus.Completed)){o.Status=OrderStatus.Completed;o.CompletedAt=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;}
            else if(lines.All(d=>d.FulfillmentStatus is OrderStatus.Shipping or OrderStatus.Completed))o.Status=OrderStatus.Shipping;
            else if(lines.Any(d=>d.FulfillmentStatus!=OrderStatus.Pending))o.Status=OrderStatus.Packing;
        } else {
            if(!OrderStatus.CanMove(o.Status,newStatus))return BadRequest("Trạng thái phải theo thứ tự: chờ xác nhận / đã thanh toán → đóng gói → đang giao → đã giao.");
            if(newStatus==OrderStatus.Cancelled){await lifecycle.CancelAsync(o,"Quản trị viên hủy đơn",ct);if(o.PaymentMethod!="cod")o.PaymentNeedsReview=true;}
            else {
                // Never move another seller's already advanced line backwards.
                if(newStatus!=o.Status && lines.Any(d=>d.FulfillmentStatus!=OrderStatus.Pending && !OrderStatus.CanMove(d.FulfillmentStatus,newStatus)))return BadRequest("Một phần đơn đã chuyển sang trạng thái khác. Hãy xử lý theo từng dòng hàng.");
                o.Status=newStatus;
                foreach(var d in lines)if(newStatus is OrderStatus.Packing or OrderStatus.Shipping or OrderStatus.Completed)d.FulfillmentStatus=newStatus;
                if(newStatus==OrderStatus.Completed)o.CompletedAt??=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;
            }
        }
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return RedirectToAction(nameof(Index));
    }
}
