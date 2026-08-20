using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using THAN_NONG_SHOP.Data;

namespace THAN_NONG_SHOP.ViewComponents
{
    public class MenuCategoryViewComponent : ViewComponent
    {
        private readonly THAN_NONG_SHOP_DbContext _context;
        private readonly IMemoryCache _cache;

        public MenuCategoryViewComponent(THAN_NONG_SHOP_DbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var categories = await _cache.GetOrCreateAsync("navigation-categories", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            }) ?? [];
            return View(categories);
        }
    }
}
