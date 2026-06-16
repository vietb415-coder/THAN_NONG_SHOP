using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers
{
  
    [Area("Admin")]
    public class ProductController : Controller
    {


        private readonly THAN_NONG_SHOP_DbContext _db;

        public ProductController(THAN_NONG_SHOP_DbContext db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
            var products = _db.Products.Include(p => p.Category).ToList();
            return View(products);
        }
    }
}
