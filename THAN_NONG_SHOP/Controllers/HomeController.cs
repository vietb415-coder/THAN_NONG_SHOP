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

        public IActionResult Index(string searchString, int? categoryId, decimal? minPrice, decimal? maxPrice)
        {
      
         
            var products = _context.Products.AsQueryable();

            
            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(p => p.Name.Contains(searchString));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                products = products.Where(p => p.categoryId == categoryId);
            }

  
            if (minPrice.HasValue)
            {
                products = products.Where(p => p.price >= minPrice.Value); 
            }
            if (maxPrice.HasValue)
            {
                products = products.Where(p => p.price <=maxPrice.Value);
            }

   
            return View(products.ToList());
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
