using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using THAN_NONG_SHOP.Data;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Models;
using System.Data;
namespace THAN_NONG_SHOP.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AdminOrdersController : Controller
    {
       private readonly THAN_NONG_SHOP_DbContext _db;
        public AdminOrdersController(THAN_NONG_SHOP_DbContext db)
        {
            _db = db;
        }
        public async Task<IActionResult> Index()
        {
            var orders = await _db.Oders.OrderByDescending(o => o.OrderDate).ToListAsync();
            return View(orders);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus)
        {
            if (!OrderStatus.All.Contains(newStatus))
            {
                return BadRequest("Trạng thái đơn hàng không hợp lệ.");
            }
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await _db.Oders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
            {
                return NotFound();
            }
            if (order.Status == OrderStatus.Cancelled && newStatus != OrderStatus.Cancelled)
            {
                return BadRequest("Đơn đã hủy và hoàn kho không thể mở lại.");
            }

            if (newStatus == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled)
            {
                await RestoreInventoryAsync(order.Id);
            }

            order.Status = newStatus;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return RedirectToAction("Index");
        }

        private async Task RestoreInventoryAsync(int orderId)
        {
            var quantities = await _db.OderDetails
                .Where(detail => detail.OderId == orderId)
                .GroupBy(detail => detail.ProductId)
                .Select(group => new { ProductId = group.Key, Quantity = group.Sum(detail => detail.Quantity) })
                .ToListAsync();

            var productIds = quantities.Select(item => item.ProductId).ToArray();
            var products = await _db.Products.Where(product => productIds.Contains(product.Id)).ToListAsync();
            foreach (var item in quantities)
            {
                var product = products.FirstOrDefault(candidate => candidate.Id == item.ProductId);
                if (product != null) product.stockQuantity += item.Quantity;
            }
        }

    }
}
