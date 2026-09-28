using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
namespace THAN_NONG_SHOP.Areas.Admin.Controllers;
[Area("Admin"),Authorize(Roles="Admin,Seller")]
public sealed class HomeController(THAN_NONG_SHOP_DbContext db):Controller
{
    public async Task<IActionResult> Index(string period="month",DateTime? date=null,CancellationToken ct=default)
    {
        if(period is not ("day" or "week" or "month" or "quarter"))period="month";
        var(start,end)=ShopRules.Period(period,date??ShopRules.Today);
        var username=User.FindFirstValue(ClaimTypes.NameIdentifier);var seller=User.IsInRole("Seller");
        var lines=await db.OderDetails.AsNoTracking().Include(d=>d.Product).Include(d=>d.Oder).Where(d=>d.Oder!=null && d.Oder.Status==OrderStatus.Completed && (d.Oder.CompletedAt??d.Oder.OrderDate)>=start && (d.Oder.CompletedAt??d.Oder.OrderDate)<end && (!seller || d.SellerUserName==username)).ToListAsync(ct);
        var orders=lines.Select(d=>d.Oder!).DistinctBy(o=>o.Id).ToList();
        var report=new SalesReport{Period=period,Start=start,End=end,Orders=orders.Count,Revenue=seller?lines.Sum(d=>d.Price*d.Quantity):orders.Sum(o=>o.TotalPrice)};
        for(var day=start;day<end;day=day.AddDays(1)){
            var ds=day;var de=day.AddDays(1);
            var value=seller?lines.Where(d=>(d.Oder!.CompletedAt??d.Oder.OrderDate)>=ds && (d.Oder.CompletedAt??d.Oder.OrderDate)<de).Sum(d=>d.Price*d.Quantity):orders.Where(o=>(o.CompletedAt??o.OrderDate)>=ds && (o.CompletedAt??o.OrderDate)<de).Sum(o=>o.TotalPrice);
            report.Points.Add(new(day.ToString("dd/MM"),value));
        }
        report.TopProducts=lines.GroupBy(d=>d.ProductId).Select(g=>new TopProduct(g.Key,g.First().Product?.Name??"Sản phẩm",g.Sum(d=>d.Quantity),g.Sum(d=>d.Quantity*d.Price))).OrderByDescending(p=>p.Quantity).ThenBy(p=>p.Id).Take(5).ToList();
        var today=ShopRules.Today;var limit=today.AddDays(3);
        report.NearExpiry=await db.ProductBatches.AsNoTracking().Include(b=>b.Product).Where(b=>b.RemainingQuantity>0 && b.ExpiryDate>=today && b.ExpiryDate<limit && (!seller || b.Product.SellerUserName==username)).OrderBy(b=>b.ExpiryDate).ToListAsync(ct);
        return View(report);
    }
}
