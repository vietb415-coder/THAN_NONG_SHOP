using Microsoft.AspNetCore.Mvc;
using System.Linq;
using THAN_NONG_SHOP.Data;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _db;

        public CategoryController(THAN_NONG_SHOP_DbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            var categories = _db.Categories.ToList();
            return View(categories);
        }
}
}
