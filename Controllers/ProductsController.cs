using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;

namespace THAN_NONG_SHOP.Controllers
{
    public class ProductsController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;

        public ProductsController(THAN_NONG_SHOP_DbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index(string? searchString, int? categoryId, decimal? minPrice, decimal? maxPrice)
        {
            var products = _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();
                products = products.Where(p => p.Name.Contains(keyword));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                products = products.Where(p => p.categoryId == categoryId.Value);
            }

            if (minPrice.HasValue)
            {
                products = products.Where(p => p.price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                products = products.Where(p => p.price <= maxPrice.Value);
            }

            ViewBag.Categories = _context.Categories.AsNoTracking().ToList();
            ViewBag.SearchString = searchString;
            ViewBag.CategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            return View(products.OrderBy(p => p.Name).ToList());
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            var product = _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
    }
}
