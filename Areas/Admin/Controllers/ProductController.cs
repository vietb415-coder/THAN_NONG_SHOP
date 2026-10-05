using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Security.Claims;
using System.Linq;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Seller")]
    public class ProductController : Controller
    {
        private IQueryable<Product> Owned => User.IsInRole("Admin") ? _db.Products : _db.Products.Where(p=>p.SellerUserName==User.FindFirstValue(ClaimTypes.NameIdentifier));
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
            var products = Owned.Include(p => p.Category).AsQueryable();

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
        public IActionResult Create([Bind("Name,Description,price,stockQuantity,categoryId,farmerStory,Unit,Certification")] Product product, IFormFile? file, string batchCode, DateTime? harvestDate, DateTime? expiryDate)
        {
            ValidateImage(file);
            if(file == null) ModelState.AddModelError("file","Vui lòng chọn ảnh sản phẩm.");
            if(harvestDate.HasValue && expiryDate.HasValue && expiryDate.Value.Date<=harvestDate.Value.Date)
                ModelState.AddModelError("expiryDate","Hạn sử dụng phải lớn hơn ngày thu hoạch.");
            else if(!ShopRules.ValidBatch(batchCode,harvestDate,expiryDate,product.stockQuantity)) ModelState.AddModelError("batchCode","Nhập mã lô, số lượng dương, ngày thu hoạch và hạn dùng hợp lệ (hạn dùng sau thu hoạch, chưa hết hạn).");
            if(!_db.Categories.Any(c=>c.Id==product.categoryId)) ModelState.AddModelError("categoryId","Danh mục không hợp lệ.");
            if(_db.ProductBatches.Any(b=>b.Code==batchCode))ModelState.AddModelError("batchCode","Mã lô đã tồn tại.");
            if (ModelState.IsValid)
            {
                if (file != null)
                {
                    product.ImageUrl = SaveImage(file);
                }

                product.SellerUserName=User.IsInRole("Seller") ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
                product.Batches.Add(new ProductBatch{Code=batchCode.Trim(),HarvestDate=harvestDate!.Value.Date,ExpiryDate=expiryDate!.Value.Date,RemainingQuantity=product.stockQuantity,IsNearExpiry=ShopRules.NearExpiry(expiryDate,ShopRules.Today)});
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

            var product = Owned.FirstOrDefault(p=>p.Id==id);
            if (product == null) return NotFound();

            ViewBag.CategoryList = _db.Categories.ToList();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit([Bind("Id,Name,Description,price,categoryId,farmerStory,Unit,Certification")] Product product, IFormFile? file)
        {
            var existingProduct = Owned.Include(p=>p.Batches).FirstOrDefault(p=>p.Id==product.Id);
            if (existingProduct == null) return NotFound();

            // Form không gửi ImageUrl; giữ ảnh hiện tại nếu quản trị viên không chọn ảnh mới.
            product.ImageUrl = existingProduct.ImageUrl;
            ValidateImage(file);
            if(!_db.Categories.Any(c=>c.Id==product.categoryId)) ModelState.AddModelError("categoryId","Danh mục không hợp lệ.");

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
                existingProduct.Unit=product.Unit;
                existingProduct.Certification=product.Certification;
                existingProduct.farmerStory=product.farmerStory;
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

            var product = Owned.Include(p => p.Category).FirstOrDefault(p => p.Id == id);
            if (product == null) return NotFound();

            return View(product);
        }


        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePost(int id)
        {
            var productFormDb = Owned.FirstOrDefault(p=>p.Id==id);
            if (productFormDb == null) return NotFound();

            if (_db.OderDetails.Any(d=>d.ProductId==id)) {TempData["ProductError"]="Sản phẩm đã có đơn hàng; không thể xóa lịch sử giao dịch.";return RedirectToAction(nameof(Index));}
            _db.ProductBatches.RemoveRange(_db.ProductBatches.Where(b=>b.ProductId==id));
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

        [HttpGet]
        public IActionResult Batches(int id) { var p=Owned.Include(p=>p.Batches).FirstOrDefault(p=>p.Id==id);return p==null?NotFound():View(p); }
        [HttpPost,ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBatchDates(int id,int batchId,DateTime? harvestDate,DateTime? expiryDate)
        {
            await using var tx=await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var p=await Owned.Include(p=>p.Batches).FirstOrDefaultAsync(p=>p.Id==id);if(p==null)return NotFound();
            var batch=p.Batches.FirstOrDefault(b=>b.Id==batchId);if(batch==null)return NotFound();
            if(!harvestDate.HasValue || !expiryDate.HasValue || expiryDate.Value.Date<=harvestDate.Value.Date) {
                TempData["BatchError"]="Hạn sử dụng phải lớn hơn ngày thu hoạch. Vui lòng nhập đầy đủ hai ngày.";
                return RedirectToAction(nameof(Batches),new{id});
            }
            batch.HarvestDate=harvestDate.Value.Date;batch.ExpiryDate=expiryDate.Value.Date;batch.IsLegacy=false;
            batch.IsNearExpiry=batch.RemainingQuantity>0 && ShopRules.NearExpiry(batch.ExpiryDate,ShopRules.Today);
            p.stockQuantity=p.Batches.Where(b=>b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today).Sum(b=>b.RemainingQuantity);
            await _db.SaveChangesAsync();await tx.CommitAsync();
            return RedirectToAction(nameof(Batches),new{id});
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBatch(int id,string code,DateTime? harvestDate,DateTime? expiryDate,int quantity) {
            await using var tx=await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var p=await Owned.Include(p=>p.Batches).FirstOrDefaultAsync(p=>p.Id==id);if(p==null)return NotFound();
            if(!ShopRules.ValidBatch(code,harvestDate,expiryDate,quantity) || _db.ProductBatches.Any(b=>b.Code==code.Trim())) {
                TempData["BatchError"]="Mã lô phải duy nhất; số lượng dương; ngày hết hạn sau thu hoạch và chưa hết hạn.";return RedirectToAction(nameof(Batches),new{id});
            }
            p.Batches.Add(new ProductBatch{Code=code.Trim(),HarvestDate=harvestDate!.Value.Date,ExpiryDate=expiryDate!.Value.Date,RemainingQuantity=quantity,IsNearExpiry=ShopRules.NearExpiry(expiryDate,ShopRules.Today)});
            p.stockQuantity=p.Batches.Where(b=>b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today).Sum(b=>b.RemainingQuantity);
            await _db.SaveChangesAsync();await tx.CommitAsync();return RedirectToAction(nameof(Batches),new{id});
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
