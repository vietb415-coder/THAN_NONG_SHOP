using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
using System.Security.Claims;

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
                .Include(p => p.Category).Include(p=>p.Batches)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();
                products = products.Where(p => (p.Name.Contains(keyword) || p.Description.Contains(keyword)));
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

            var results=await products.OrderBy(p=>p.Name).ToListAsync(cancellationToken);
            var today=ShopRules.Today;
            foreach(var p in results)p.stockQuantity=p.Batches.Where(b=>b.ExpiryDate==null || b.ExpiryDate>=today).Sum(b=>b.RemainingQuantity);
            if(results.Count==0) {
                var topIds=await _context.OderDetails.Where(d=>d.Oder!=null && d.Oder.Status==OrderStatus.Completed).GroupBy(d=>d.ProductId).OrderByDescending(g=>g.Sum(d=>d.Quantity)).Select(g=>g.Key).Take(5).ToListAsync(cancellationToken);
                var candidates=await _context.Products.AsNoTracking().Where(p=>topIds.Contains(p.Id)).ToListAsync(cancellationToken);
                ViewBag.Suggestions=topIds.Select(id=>candidates.First(p=>p.Id==id)).ToList();
            }
            if(Request.Headers["X-Requested-With"]=="XMLHttpRequest")return PartialView("_Results",results);
            return View(results);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .Include(p => p.Category).Include(p=>p.Batches)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (product == null)
            {
                return NotFound();
            }

            product.stockQuantity=product.Batches.Where(b=>b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today).Sum(b=>b.RemainingQuantity);
            var reviews = await _context.ProductReviews.AsNoTracking().Include(r=>r.Media)
                .Where(review => review.ProductId == id)
                .OrderByDescending(review => review.UpdatedAt ?? review.CreatedAt)
                .ToListAsync(cancellationToken);
            var userName = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var canReview = !string.IsNullOrWhiteSpace(userName) && await _context.OderDetails.AsNoTracking().AnyAsync(detail =>
                detail.ProductId == id && detail.Oder != null && detail.Oder.UserName == userName &&
                detail.Oder.Status == OrderStatus.Completed, cancellationToken);

            return View(new ProductDetailsViewModel
            {
                Product = product,
                Reviews = reviews,
                AverageRating = reviews.Count == 0 ? 0 : reviews.Average(review => review.Rating),
                CanReview = canReview,
                CurrentUserReview = reviews.FirstOrDefault(review => review.UserName == userName)
            });
        }
    }
}
