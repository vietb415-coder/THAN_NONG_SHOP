using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
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
        private const long MaxImageSize = 5 * 1024 * 1024;
        private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png", "image/webp"];
        private readonly THAN_NONG_SHOP_DbContext _db;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(THAN_NONG_SHOP_DbContext db, IWebHostEnvironment webHostEnvironment)
        {
            _db = db;
            _webHostEnvironment = webHostEnvironment;
        }


        public IActionResult Index(int? categoryId)
        {
            var products = _db.Products.Include(p => p.Category).AsQueryable();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                products = products.Where(p => p.categoryId == categoryId.Value);
                ViewBag.SelectedCategory = _db.Categories
                    .Where(c => c.Id == categoryId.Value)
                    .Select(c => c.Name)
                    .FirstOrDefault();
            }

            return View(products.ToList());
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
            ValidateImage(file);
            if (ModelState.IsValid)
            {
                if (file != null)
                {
                    product.ImageUrl = SaveImage(file);
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
            var existingProduct = _db.Products.Find(product.Id);
            if (existingProduct == null) return NotFound();

            // Form không gửi ImageUrl; giữ ảnh hiện tại nếu quản trị viên không chọn ảnh mới.
            product.ImageUrl = existingProduct.ImageUrl;
            ValidateImage(file);

            if (ModelState.IsValid)
            {
                string wwwRootPath = _webHostEnvironment.WebRootPath;

                if (file != null)
                {
                    var newImageUrl = SaveImage(file);

                    if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                    {
                        var oldImagePath = Path.Combine(wwwRootPath, existingProduct.ImageUrl.TrimStart('\\', '/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    existingProduct.ImageUrl = newImageUrl;
                }

                // Chỉ cập nhật dữ liệu có trên form, tránh ghi null vào ảnh và câu chuyện nhà nông.
                existingProduct.Name = product.Name;
                existingProduct.Description = product.Description;
                existingProduct.categoryId = product.categoryId;
                existingProduct.price = product.price;
                existingProduct.stockQuantity = product.stockQuantity;
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

        private void ValidateImage(IFormFile? file)
        {
            if (file == null) return;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (file.Length <= 0 || file.Length > MaxImageSize)
            {
                ModelState.AddModelError("file", "Ảnh phải có dung lượng từ 1 byte đến 5 MB.");
                return;
            }

            if (!AllowedImageExtensions.Contains(extension) ||
                !AllowedImageContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase) ||
                !HasValidImageSignature(file, extension))
            {
                ModelState.AddModelError("file", "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP hợp lệ.");
            }
        }

        private string SaveImage(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var productPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
            Directory.CreateDirectory(productPath);

            using var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.CreateNew);
            file.CopyTo(fileStream);
            return "/images/products/" + fileName;
        }

        private static bool HasValidImageSignature(IFormFile file, string extension)
        {
            Span<byte> header = stackalloc byte[12];
            using var stream = file.OpenReadStream();
            var bytesRead = stream.Read(header);

            return extension switch
            {
                ".jpg" or ".jpeg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => bytesRead >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                ".webp" => bytesRead >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
                _ => false
            };
        }
    }
}
