using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string CartSessionKey = "CartItems";

        public CartController(THAN_NONG_SHOP_DbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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
                var cart = SystemTextJson.JsonSerializer.Deserialize<List<CartItem>>(cookieData, _jsonOptions);
                return cart ?? new List<CartItem>();
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
            var simplifiedCart = cartItems.Select(item => new CartItem
            {
                // Gán Product thô chỉ có Id hoặc dùng trực tiếp thuộc tính ProductId nếu class của bạn có sẵn
                Product = new Product { Id = item.Product.Id },
                Quantity = item.Quantity
            }).ToList();

            var cookieData = SystemTextJson.JsonSerializer.Serialize(simplifiedCart);
            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(30),
                HttpOnly = true,
                Secure = false // Đổi thành false để test mượt ở localhost, khi lên hosting SSL đổi thành true
            };
            HttpContext.Response.Cookies.Append(CartSessionKey, cookieData, cookieOptions);
        }

        // Trang danh sách giỏ hàng công khai công khai
        public IActionResult Index()
        {
            var cartItems = GetCartItems();

            // Nạp thông tin chi tiết sản phẩm từ Database dựa trên Id đã lưu trong Cookie
            foreach (var item in cartItems)
            {
                if (item.Product != null)
                {
                    item.Product = _context.Products.Find(item.Product.Id);
                }
            }

            ViewBag.Total = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);
            return View(cartItems);
        }

        [HttpPost]
        [Authorize(Roles = "User")] // CHỮ "User" VIẾT HOA: Đã sửa đồng bộ với phân quyền lúc Login
        public IActionResult AddToCart(int productId, int quantity)
        {
            var product = _context.Products.Find(productId);
            if (product == null)
            {
                return NotFound();
            }

            var cartItems = GetCartItems();
            var existingItem = cartItems.FirstOrDefault(item => item.Product != null && item.Product.Id == productId);

            if (existingItem == null)
            {
                // Thêm mới sản phẩm vào giỏ
                cartItems.Add(new CartItem { Product = new Product { Id = productId }, Quantity = quantity });
            }
            else
            {
                // Cộng dồn số lượng
                existingItem.Quantity += quantity;
            }

            SaveCartItems(cartItems);

            // Sau khi thêm thành công, chuyển hướng thẳng sang trang hiển thị Giỏ hàng
            return RedirectToAction(nameof(Index));
        }

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
                    existingItem.Quantity = quantity;
                }
                SaveCartItems(cartItems);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "User")]
        [HttpGet]
        public IActionResult Checkout()
        {
            var cartItems = GetCartItems();
            if (!cartItems.Any())
            {
                return RedirectToAction("Index", "Home");
            }

            // Nạp lại dữ liệu sản phẩm để hiển thị số tiền chính xác ở trang check out
            foreach (var item in cartItems)
            {
                if (item.Product != null)
                {
                    item.Product = _context.Products.Find(item.Product.Id);
                }
            }

            var currentUsername = User.Identity?.Name;
            var userProfile = _context.Users.FirstOrDefault(u => u.UserName == currentUsername);

            ViewBag.UserFullName = userProfile?.Fullname ?? "";
            ViewBag.UserPhone = userProfile?.Phone ?? "";
            ViewBag.Total = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);

            return View(cartItems); // Nên truyền danh sách mặt hàng để hiển thị tóm tắt đơn hàng
        }

        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(string customerName, string shippingAddress, string shippingPhone, string paymentMethod)
        {
            var cartItems = GetCartItems();

            if (cartItems == null || cartItems.Count == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Nạp lại giá từ DB để tính tổng tiền chính xác, tránh việc người dùng gian lận thay đổi dữ liệu cookie
            foreach (var item in cartItems)
            {
                if (item.Product != null)
                {
                    item.Product = _context.Products.Find(item.Product.Id);
                }
            }

            var currentUsername = User.Identity?.Name;
            var userProfile = _context.Users.FirstOrDefault(u => u.UserName == currentUsername);

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

            if (isPayOS)
            {
                var clientId = _configuration["PayOS:ClientId"];
                var apiKey = _configuration["PayOS:ApiKey"];
                var checksumKey = _configuration["PayOS:ChecksumKey"];
                if (string.IsNullOrWhiteSpace(clientId)) clientId = Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID");
                if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("PAYOS_API_KEY");
                if (string.IsNullOrWhiteSpace(checksumKey)) checksumKey = Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY");

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(checksumKey))
                {
                    order.Status = "Chưa cấu hình PayOS";
                    await _context.SaveChangesAsync();
                    TempData["CheckoutError"] = "PayOS chưa được cấu hình. Hãy thêm Client ID, API Key và Checksum Key.";
                    return RedirectToAction(nameof(Checkout));
                }

                try
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var payOS = new PayOSClient(clientId, apiKey, checksumKey);
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
