using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using SystemTextJson = System.Text.Json;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
using PayOS;
using PayOS.Models.V2.PaymentRequests;

namespace THAN_NONG_SHOP.Controllers
{
    public class CartController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IDataProtector _cartProtector;
        private const string CartSessionKey = "CartItems";
        private const int MaxQuantityPerProduct = 999;

        private sealed class CartCookieItem
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        public CartController(THAN_NONG_SHOP_DbContext context, IConfiguration configuration, IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _configuration = configuration;
            _cartProtector = dataProtectionProvider.CreateProtector("THAN_NONG_SHOP.Cart.v1");
        }

        private readonly SystemTextJson.JsonSerializerOptions _jsonOptions = new SystemTextJson.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Lấy danh sách giỏ hàng thô từ Cookie (Chỉ có Id sản phẩm và số lượng)
        private List<CartItem> GetCartItems()
        {
            var cookieData = HttpContext.Request.Cookies[CartSessionKey];
            if (string.IsNullOrEmpty(cookieData))
            {
                return new List<CartItem>();
            }
            try
            {
                var json = _cartProtector.Unprotect(cookieData);
                var storedItems = SystemTextJson.JsonSerializer.Deserialize<List<CartCookieItem>>(json, _jsonOptions);
                return storedItems?
                    .Where(item => item.ProductId > 0 && item.Quantity > 0)
                    .Select(item => new CartItem
                    {
                        Product = new Product { Id = item.ProductId },
                        Quantity = item.Quantity
                    }).ToList() ?? new List<CartItem>();
            }
            catch
            {
                return new List<CartItem>();
            }
        }

        // Lưu giỏ hàng xuống cookie
        private void SaveCartItems(List<CartItem> cartItems)
        {
            // Để cookie nhẹ và không lỗi Entity, ta chỉ map lại dữ liệu thô để sấy chuỗi JSON
            var simplifiedCart = cartItems
                .Where(item => item.Product != null && item.Quantity > 0)
                .Select(item => new CartCookieItem
            {
                ProductId = item.Product!.Id,
                Quantity = item.Quantity
            }).ToList();

            var cookieData = _cartProtector.Protect(SystemTextJson.JsonSerializer.Serialize(simplifiedCart));
            var cookieOptions = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            };
            HttpContext.Response.Cookies.Append(CartSessionKey, cookieData, cookieOptions);
        }

        private async Task LoadCartProductsAsync(List<CartItem> cartItems, CancellationToken cancellationToken = default)
        {
            var productIds = cartItems
                .Where(item => item.Product != null)
                .Select(item => item.Product!.Id)
                .Distinct()
                .ToArray();

            var productsById = await _context.Products
                .AsNoTracking()
                .Where(product => productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);

            foreach (var item in cartItems)
            {
                if (item.Product != null && productsById.TryGetValue(item.Product.Id, out var product))
                {
                    item.Product = product;
                }
                else
                {
                    item.Product = null;
                }
            }

            cartItems.RemoveAll(item => item.Product == null);
        }

        // Trang danh sách giỏ hàng công khai công khai
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var cartItems = GetCartItems();
            await LoadCartProductsAsync(cartItems, cancellationToken);

            ViewBag.Total = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);
            return View(cartItems);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User")] // CHỮ "User" VIẾT HOA: Đã sửa đồng bộ với phân quyền lúc Login
        public IActionResult AddToCart(int productId, int quantity)
        {
            var product = _context.Products.Find(productId);
            if (product == null)
            {
                return NotFound();
            }

            if (quantity <= 0 || product.stockQuantity <= 0)
            {
                TempData["CartError"] = "Sản phẩm đã hết hàng hoặc số lượng không hợp lệ.";
                return RedirectToAction("Details", "Products", new { id = productId });
            }

            var cartItems = GetCartItems();
            var existingItem = cartItems.FirstOrDefault(item => item.Product != null && item.Product.Id == productId);

            var currentQuantity = existingItem?.Quantity ?? 0;
            var requestedQuantity = Math.Min(currentQuantity + quantity, MaxQuantityPerProduct);
            if (requestedQuantity > product.stockQuantity)
            {
                TempData["CartError"] = $"Sản phẩm chỉ còn {product.stockQuantity} sản phẩm trong kho.";
                return RedirectToAction("Details", "Products", new { id = productId });
            }

            if (existingItem == null)
            {
                // Thêm mới sản phẩm vào giỏ
                cartItems.Add(new CartItem { Product = new Product { Id = productId }, Quantity = requestedQuantity });
            }
            else
            {
                // Cộng dồn số lượng
                existingItem.Quantity = requestedQuantity;
            }

            SaveCartItems(cartItems);

            // Sau khi thêm thành công, chuyển hướng thẳng sang trang hiển thị Giỏ hàng
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateQuantity(int productId, int quantity)
        {
            var cartItems = GetCartItems();
            var existingItem = cartItems.FirstOrDefault(item => item.Product != null && item.Product.Id == productId);

            if (existingItem != null)
            {
                if (quantity <= 0)
                {
                    cartItems.Remove(existingItem);
                }
                else
                {
                    var product = _context.Products.AsNoTracking().FirstOrDefault(p => p.Id == productId);
                    if (product == null)
                    {
                        cartItems.Remove(existingItem);
                    }
                    else if (quantity > product.stockQuantity || quantity > MaxQuantityPerProduct)
                    {
                        TempData["CartError"] = $"Sản phẩm chỉ còn {product.stockQuantity} sản phẩm trong kho.";
                    }
                    else
                    {
                        existingItem.Quantity = quantity;
                    }
                }
                SaveCartItems(cartItems);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "User")]
        [HttpGet]
        public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
        {
            var cartItems = GetCartItems();
            if (!cartItems.Any())
            {
                return RedirectToAction("Index", "Home");
            }

            await LoadCartProductsAsync(cartItems, cancellationToken);

            cartItems.RemoveAll(item => item.Product == null || item.Quantity <= 0);
            if (!cartItems.Any()) return RedirectToAction(nameof(Index));

            var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userProfile = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == currentUsername, cancellationToken);

            ViewBag.UserFullName = userProfile?.Fullname ?? "";
            ViewBag.UserPhone = userProfile?.Phone ?? "";
            ViewBag.Total = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);

            return View(cartItems); // Nên truyền danh sách mặt hàng để hiển thị tóm tắt đơn hàng
        }

        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(string customerName, string shippingAddress, string shippingPhone, string paymentMethod, CancellationToken cancellationToken)
        {
            var cartItems = GetCartItems();

            if (cartItems == null || cartItems.Count == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Nạp lại giá từ DB trong một truy vấn để tính tổng tiền chính xác.
            await LoadCartProductsAsync(cartItems, cancellationToken);

            cartItems.RemoveAll(item => item.Product == null || item.Quantity <= 0);
            if (!cartItems.Any())
            {
                TempData["CheckoutError"] = "Giỏ hàng không còn sản phẩm hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var item in cartItems)
            {
                if (item.Quantity > item.Product!.stockQuantity || item.Quantity > MaxQuantityPerProduct)
                {
                    TempData["CartError"] = $"Số lượng {item.Product.Name} vượt quá tồn kho hiện tại.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userProfile = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == currentUsername, cancellationToken);

            if (userProfile == null)
            {
                return NotFound("Không tìm thấy thông tin tài khoản.");
            }

            if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(shippingPhone) || string.IsNullOrWhiteSpace(shippingAddress))
            {
                TempData["CheckoutError"] = "Vui lòng nhập đầy đủ thông tin nhận hàng.";
                return RedirectToAction(nameof(Checkout));
            }

            var isPayOS = string.Equals(paymentMethod, "payos", StringComparison.OrdinalIgnoreCase);

            string? clientId = null;
            string? apiKey = null;
            string? checksumKey = null;
            if (isPayOS)
            {
                clientId = _configuration["PayOS:ClientId"];
                apiKey = _configuration["PayOS:ApiKey"];
                checksumKey = _configuration["PayOS:ChecksumKey"];
                if (string.IsNullOrWhiteSpace(clientId)) clientId = Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID");
                if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("PAYOS_API_KEY");
                if (string.IsNullOrWhiteSpace(checksumKey)) checksumKey = Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY");
                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(checksumKey))
                {
                    TempData["CheckoutError"] = "PayOS chưa được cấu hình. Hãy chọn thanh toán khi nhận hàng hoặc cấu hình PayOS.";
                    return RedirectToAction(nameof(Checkout));
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var order = new Oder
            {
                OrderDate = DateTime.Now,
                UserName = currentUsername,
                CustomerName = customerName.Trim(),
                Address = shippingAddress.Trim(),
                PhoneNumber = shippingPhone.Trim(),
                TotalPrice = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity),
                Status = isPayOS ? "Chờ thanh toán PayOS" : "Đang chờ xử lý",
            };

            _context.Add(order);
            await _context.SaveChangesAsync(); // Lưu để lấy được order.Id tự tăng

            foreach (var item in cartItems)
            {
                if (item.Product == null) continue;
                var trackedProduct = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.Product.Id);
                if (trackedProduct == null || item.Quantity > trackedProduct.stockQuantity)
                {
                    await transaction.RollbackAsync();
                    TempData["CartError"] = $"Sản phẩm {item.Product.Name} không còn đủ hàng.";
                    return RedirectToAction(nameof(Index));
                }

                trackedProduct.stockQuantity -= item.Quantity;
                var orderDetail = new OderDetail
                {
                    OderId = order.Id,
                    ProductId = item.Product.Id,
                    Quantity = item.Quantity,
                    Price = item.Product.price
                };
                _context.Add(orderDetail);
            }
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (isPayOS)
            {
                try
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var payOS = new PayOSClient(clientId!, apiKey!, checksumKey!);
                    var paymentRequest = new CreatePaymentLinkRequest
                    {
                        OrderCode = order.Id,
                        Amount = decimal.ToInt32(order.TotalPrice),
                        Description = $"Don hang {order.Id}",
                        ReturnUrl = $"{baseUrl}/Payment/Return",
                        CancelUrl = $"{baseUrl}/Payment/Cancel"
                    };
                    var paymentLink = await payOS.PaymentRequests.CreateAsync(paymentRequest);
                    return Redirect(paymentLink.CheckoutUrl);
                }
                catch (Exception)
                {
                    order.Status = "Lỗi tạo thanh toán PayOS";
                    foreach (var item in cartItems)
                    {
                        if (item.Product == null) continue;
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.Product.Id);
                        if (product != null) product.stockQuantity += item.Quantity;
                    }
                    await _context.SaveChangesAsync();
                    TempData["CheckoutError"] = "Không thể tạo liên kết thanh toán PayOS. Vui lòng kiểm tra khóa cấu hình và thử lại.";
                    return RedirectToAction(nameof(Checkout));
                }
            }

            // Xóa sạch giỏ hàng sau khi đặt thành công
            HttpContext.Response.Cookies.Delete(CartSessionKey);

            return RedirectToAction("OrderSuccess");
        }

        public IActionResult OrderSuccess()
        {
            return View();
        }
    }
}
