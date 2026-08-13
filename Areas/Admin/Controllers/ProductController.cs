using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.IO;
using System.Linq;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _db;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(THAN_NONG_SHOP_DbContext db, IWebHostEnvironment webHostEnvironment)
        {
            _db = db;
            _webHostEnvironment = webHostEnvironment;
        }


        public IActionResult Index()
        {
            var products = _db.Products.Include(p => p.Category).ToList();
            return View(products);
        }

 
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.CategoryList = _db.Categories.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Product product, IFormFile? file)
        {
            if (ModelState.IsValid)
            {
                if (file != null)
                {
                    string wwwRootPath = _webHostEnvironment.WebRootPath;
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);

                    
                    string productPath = Path.Combine(wwwRootPath, @"images\products");

                    if (!Directory.Exists(productPath))
                    {
                        Directory.CreateDirectory(productPath);
                    }

                    using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }
                    product.ImageUrl = @"/images/products/" + fileName;
                }

                _db.Products.Add(product);
                _db.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CategoryList = _db.Categories.ToList();
            return View(product);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (id == 0) return NotFound();

            var product = _db.Products.Find(id);
            if (product == null) return NotFound();

            ViewBag.CategoryList = _db.Categories.ToList();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Product product, IFormFile? file)
        {
            if (ModelState.IsValid)
            {
                string wwwRootPath = _webHostEnvironment.WebRootPath;

                if (file != null)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    string productPath = Path.Combine(wwwRootPath, @"images\products");

                    if (!string.IsNullOrEmpty(product.ImageUrl))
                    {
                        var oldImagePath = Path.Combine(wwwRootPath, product.ImageUrl.TrimStart('\\', '/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    if (!Directory.Exists(productPath))
                    {
                        Directory.CreateDirectory(productPath);
                    }

                    using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }

                    product.ImageUrl = @"/images/products/" + fileName;
                }

                _db.Products.Update(product);
                _db.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CategoryList = _db.Categories.ToList();
            return View(product);
        }


        [HttpGet]
        public IActionResult Delete(int id)
        {
            if (id == 0) return NotFound();

            var product = _db.Products.Include(p => p.Category).FirstOrDefault(p => p.Id == id);
            if (product == null) return NotFound();

            return View(product);
        }


        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePost(int id)
        {
            var productFormDb = _db.Products.Find(id);
            if (productFormDb == null) return NotFound();

            if (!string.IsNullOrEmpty(productFormDb.ImageUrl))
            {
                string wwwRootPath = _webHostEnvironment.WebRootPath;
                var imagePath = Path.Combine(wwwRootPath, productFormDb.ImageUrl.TrimStart('\\', '/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }

            _db.Products.Remove(productFormDb);
            _db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
    }
}