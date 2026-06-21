using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using THAN_NONG_SHOP.Data;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;

        public HomeController(THAN_NONG_SHOP_DbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // 1. Tổng số đơn hàng trong hệ thống
            ViewBag.TotalOrders = _context.Oders.Count();

            // 2. Tổng doanh thu từ các đơn hàng đã hoàn thành ("Đã hoàn thành")
            ViewBag.TotalRevenue = _context.Oders
                .Where(o => o.Status == "Đã hoàn thành")
                .Sum(o => (decimal?)o.TotalPrice) ?? 0;

            // 3. Tổng số lượng sản phẩm nông sản hiện có trong kho
            ViewBag.TotalStock = _context.Products.Any() 
                ? _context.Products.Sum(p => p.stockQuantity) 
                : 0;

            // 4. Tổng số tài khoản khách hàng đã đăng ký (RoleId == 2)
            ViewBag.TotalCustomers = _context.Users.Count(u => u.RoleId == 2);

            return View();
        }
    }
}
