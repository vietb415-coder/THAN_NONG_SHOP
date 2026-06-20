using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using THAN_NONG_SHOP.Data;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
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
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus)
        {
            var order = await _db.Oders.FindAsync(orderId);
            if (order == null)
            {
                return NotFound();
            }
            order.Status = newStatus;
            await _db.SaveChangesAsync();
            return RedirectToAction("Index");
        }

    }
}
