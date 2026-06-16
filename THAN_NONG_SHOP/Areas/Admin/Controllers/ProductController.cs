using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.IO;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers
{
  
    [Area("Admin")]
    public class ProductController : Controller
    {


        private readonly THAN_NONG_SHOP_DbContext _db;

        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(THAN_NONG_SHOP_DbContext db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
            var products = _db.Products.Include(p => p.Category).ToList();
            return View(products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Categories = _db.Categories.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Product product, IFormFile? file)
        {
            if (ModelState.IsValid)
            {
                if( file != null)
                {
                    string wwwRootPath = _webHostEnvironment.WebRootPath;
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    string filePath = Path.Combine(wwwRootPath, "images", fileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }
                    product.ImageUrl = "/images/" + fileName;
                }
                _db.Products.Add(product);
                _db.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Categories = _db.Categories.ToList();
            return View(product);
        }
    }
}
