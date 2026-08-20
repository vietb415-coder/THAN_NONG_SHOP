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
        public async Task<IActionResult> Index(string? searchString, int? categoryId, decimal? minPrice, decimal? maxPrice, CancellationToken cancellationToken)
        {
            minPrice = minPrice is >= 0 ? minPrice : null;
            maxPrice = maxPrice is >= 0 ? maxPrice : null;

            if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
            {
                (minPrice, maxPrice) = (maxPrice, minPrice);
            }

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

            ViewBag.Categories = await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
            ViewBag.SearchString = searchString;
            ViewBag.CategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            return View(await products.OrderBy(p => p.Name).ToListAsync(cancellationToken));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
    }
}
