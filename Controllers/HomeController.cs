using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Data;
using System.Linq;
using Microsoft.AspNetCore.Authentication;

namespace THAN_NONG_SHOP.Controllers
{
    public class HomeController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;
        public HomeController(THAN_NONG_SHOP_DbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            if (id == 0)
            {
                return NotFound();
            }
            var product = await _context.Products.AsNoTracking().Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (product == null)
            {
                return NotFound();
            }
            else
            {
                return View(product);
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
