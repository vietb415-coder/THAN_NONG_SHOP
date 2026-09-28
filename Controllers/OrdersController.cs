using System.Security.Claims;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Services;
namespace THAN_NONG_SHOP.Controllers;
[Authorize]
public sealed class OrdersController(THAN_NONG_SHOP_DbContext db,OrderLifecycle lifecycle) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await db.Oders.AsNoTracking().Where(o=>o.UserName==User.FindFirstValue(ClaimTypes.NameIdentifier)).OrderByDescending(o=>o.OrderDate).ToListAsync(ct));
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id,CancellationToken ct) {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var o=await db.Oders.FirstOrDefaultAsync(o=>o.Id==id && o.UserName==User.FindFirstValue(ClaimTypes.NameIdentifier),ct);
        if(o==null)return NotFound();
        if(o.Status!=OrderStatus.Pending) {TempData["OrderMessage"]="Chỉ có thể tự hủy đơn đang chờ xác nhận. Vui lòng liên hệ cửa hàng để được hỗ trợ.";return RedirectToAction(nameof(Index));}
        await lifecycle.CancelAsync(o,"Khách hàng hủy đơn đang chờ xác nhận",ct);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        TempData["OrderMessage"]="Đã hủy đơn và hoàn lại tồn kho.";return RedirectToAction(nameof(Index));
    }
}
