using Microsoft.AspNetCore.Mvc;
using System.Linq;
using THAN_NONG_SHOP.Data;

namespace THAN_NONG_SHOP.ViewComponents
{
    public class MenuCategoryViewComponent : ViewComponent
    {
        private readonly THAN_NONG_SHOP_DbContext _context;

        public MenuCategoryViewComponent(THAN_NONG_SHOP_DbContext context)
        {
            _context = context;
        }

        public IViewComponentResult Invoke()
        { 
            var categories = _context.Categories.ToList();
            return View(categories);
        }
    }
}