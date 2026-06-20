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

         public async Task<IActionResult> Index()
        {
            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            return View();
        }


        public IActionResult Details(int id)
        {
            if (id == 0)
            {
                return NotFound();
            }
            var product = _context.Products.Include(p => p.Category).FirstOrDefault(p => p.Id == id);
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
