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
using System.Text.RegularExpressions;
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
        private readonly ILogger<CartController> _logger;
        private const string CartSessionKey = "CartItems";
        private const string PromotionSessionKey = "AppliedPromotionCode";
        private const int MaxQuantityPerProduct = 999;

        private sealed class CartCookieItem
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        public CartController(THAN_NONG_SHOP_DbContext context, IConfiguration configuration, IDataProtectionProvider dataProtectionProvider, ILogger<CartController> logger)
        {
            _context = context;
            _configuration = configuration;
            _cartProtector = dataProtectionProvider.CreateProtector("THAN_NONG_SHOP.Cart.v1");
            _logger = logger;
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

        private async Task<PromotionResult> BuildPromotionSummaryAsync(decimal subtotal, CancellationToken cancellationToken = default)
        {
            var code = HttpContext.Session.GetString(PromotionSessionKey);
            if (string.IsNullOrWhiteSpace(code))
            {
                return PromotionCatalog.Calculate((string?)null, subtotal) with
                {
                    Message = string.Empty
                };
            }

            var userName = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var codeHash = VoucherSecurity.HashCode(code);
            var voucher = await _context.PromotionVouchers.AsNoTracking().Include(item => item.Reward).FirstOrDefaultAsync(item =>
                item.CodeHash == codeHash && item.UserName == userName && item.UsedAt == null && item.OrderId == null && item.ExpiresAt >= DateTime.UtcNow,
                cancellationToken);
            if (voucher == null)
            {
                HttpContext.Session.Remove(PromotionSessionKey);
                return PromotionCatalog.Calculate((string?)null, subtotal) with { Message = string.Empty };
            }

            var reward = voucher.Reward ?? await _context.PromotionRewards.AsNoTracking().FirstOrDefaultAsync(item => item.TemplateCode == voucher.TemplateCode, cancellationToken);
            var result = reward == null ? PromotionCatalog.Calculate(voucher.TemplateCode, subtotal) : PromotionCatalog.Calculate(reward, subtotal);
            result = result with { Code = code };
            if (!result.IsValid) HttpContext.Session.Remove(PromotionSessionKey);
            return result;
        }

        private void PopulatePriceViewData(decimal subtotal, PromotionResult promotion)
        {
            ViewBag.Subtotal = subtotal;
            ViewBag.ShippingFee = promotion.ShippingFee;
            ViewBag.Discount = promotion.DiscountAmount;
            ViewBag.Total = promotion.FinalTotal;
            ViewBag.PromotionCode = promotion.IsValid ? promotion.Code : string.Empty;
            ViewBag.PromotionMessage = promotion.IsValid ? promotion.Message : string.Empty;
            ViewBag.PromotionGift = promotion.IsValid && promotion.DiscountAmount == 0
                ? promotion.Message
                : string.Empty;
        }

        // Trang danh sách giỏ hàng công khai công khai
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var cartItems = GetCartItems();
            await LoadCartProductsAsync(cartItems, cancellationToken);

            var subtotal = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);
            PopulatePriceViewData(subtotal, await BuildPromotionSummaryAsync(subtotal, cancellationToken));
            return View(cartItems);
        }

        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyPromotion(string? promotionCode, string? returnTo, CancellationToken cancellationToken)
        {
            var cartItems = GetCartItems();
            await LoadCartProductsAsync(cartItems, cancellationToken);
            var subtotal = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);
            var normalizedCode = (promotionCode ?? string.Empty).Trim().ToUpperInvariant();
            var userName = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var codeHash = VoucherSecurity.HashCode(normalizedCode);
            var voucher = await _context.PromotionVouchers.AsNoTracking().Include(item => item.Reward).FirstOrDefaultAsync(item =>
                item.CodeHash == codeHash && item.UserName == userName && item.UsedAt == null && item.OrderId == null && item.ExpiresAt >= DateTime.UtcNow,
                cancellationToken);
            var result = voucher == null
                ? new PromotionResult(false, normalizedCode, "Voucher không tồn tại, đã hết hạn, đã sử dụng hoặc không thuộc tài khoản của bạn.", 0, subtotal > 0 ? PromotionCatalog.StandardShippingFee : 0, subtotal + (subtotal > 0 ? PromotionCatalog.StandardShippingFee : 0))
                : (voucher.Reward == null ? PromotionCatalog.Calculate(voucher.TemplateCode, subtotal) : PromotionCatalog.Calculate(voucher.Reward, subtotal)) with { Code = normalizedCode };

            if (!result.IsValid)
            {
                TempData["PromotionError"] = result.Message;
            }
            else
            {
                HttpContext.Session.SetString(PromotionSessionKey, result.Code);
                TempData["PromotionSuccess"] = $"Đã áp dụng {result.Code}: {result.Message}";
                _context.PromotionVoucherEvents.Add(new PromotionVoucherEvent { VoucherId=voucher!.Id, EventType=VoucherEventTypes.Applied, UserName=userName!, CreatedAt=DateTime.UtcNow });
                await _context.SaveChangesAsync(cancellationToken);
            }

            return RedirectToAction(string.Equals(returnTo, "checkout", StringComparison.OrdinalIgnoreCase)
                ? nameof(Checkout)
                : nameof(Index));
        }

        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemovePromotion(string? returnTo)
        {
            HttpContext.Session.Remove(PromotionSessionKey);
            TempData["CartSuccess"] = "Đã bỏ mã khuyến mãi.";
            return RedirectToAction(string.Equals(returnTo, "checkout", StringComparison.OrdinalIgnoreCase)
                ? nameof(Checkout)
                : nameof(Index));
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveFromCart(int productId)
        {
            var cartItems = GetCartItems();
            var removedCount = cartItems.RemoveAll(item =>
                item.Product != null && item.Product.Id == productId);

            if (removedCount > 0)
            {
                SaveCartItems(cartItems);
                TempData["CartSuccess"] = "Đã xóa sản phẩm khỏi giỏ hàng.";
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
            var subtotal = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);
            PopulatePriceViewData(subtotal, await BuildPromotionSummaryAsync(subtotal, cancellationToken));

            return View(cartItems); // Nên truyền danh sách mặt hàng để hiển thị tóm tắt đơn hàng
        }

        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(string customerName, string shippingAddress, string shippingPhone, string paymentMethod, CancellationToken cancellationToken)
        {
            customerName = customerName?.Trim() ?? string.Empty;
            shippingAddress = shippingAddress?.Trim() ?? string.Empty;
            shippingPhone = shippingPhone?.Trim() ?? string.Empty;
            paymentMethod = paymentMethod?.Trim() ?? string.Empty;

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

            if (customerName.Length is < 2 or > 100)
            {
                TempData["CheckoutError"] = "Tên người nhận phải có từ 2 đến 100 ký tự.";
                return RedirectToAction(nameof(Checkout));
            }
            if (!Regex.IsMatch(shippingPhone, @"^0\d{9}$"))
            {
                TempData["CheckoutError"] = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.";
                return RedirectToAction(nameof(Checkout));
            }
            if (shippingAddress.Length is < 5 or > 500)
            {
                TempData["CheckoutError"] = "Địa chỉ nhận hàng phải có từ 5 đến 500 ký tự.";
                return RedirectToAction(nameof(Checkout));
            }
            if (!string.Equals(paymentMethod, "cod", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(paymentMethod, "payos", StringComparison.OrdinalIgnoreCase))
            {
                TempData["CheckoutError"] = "Phương thức thanh toán không hợp lệ.";
                return RedirectToAction(nameof(Checkout));
            }

            var isPayOS = string.Equals(paymentMethod, "payos", StringComparison.OrdinalIgnoreCase);
            var subtotal = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);
            var promotion = await BuildPromotionSummaryAsync(subtotal, cancellationToken);

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
                CustomerName = customerName,
                Address = shippingAddress,
                PhoneNumber = shippingPhone,
                TotalPrice = promotion.FinalTotal,
                Subtotal = subtotal,
                ShippingFee = promotion.ShippingFee,
                DiscountAmount = promotion.DiscountAmount,
                Status = isPayOS ? OrderStatus.AwaitingPayment : OrderStatus.Pending,
                // PayOS yêu cầu orderCode duy nhất trên toàn bộ kênh thanh toán.
                // Không dùng Id của DB vì Id có thể lặp lại khi tạo lại database.
                PayOSOrderCode = isPayOS ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : null,
            };

            _context.Add(order);
            await _context.SaveChangesAsync(); // Lưu để lấy được order.Id tự tăng

            if (promotion.IsValid)
            {
                var voucherHash = VoucherSecurity.HashCode(promotion.Code);
                var voucher = await _context.PromotionVouchers.Include(item => item.Reward).FirstOrDefaultAsync(item =>
                    item.CodeHash == voucherHash && item.UserName == currentUsername && item.UsedAt == null && item.OrderId == null && item.ExpiresAt >= DateTime.UtcNow,
                    cancellationToken);
                if (voucher == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    HttpContext.Session.Remove(PromotionSessionKey);
                    TempData["CheckoutError"] = "Voucher vừa hết hiệu lực hoặc đã được sử dụng. Vui lòng kiểm tra lại đơn hàng.";
                    return RedirectToAction(nameof(Checkout));
                }
                voucher.OrderId = order.Id;
                voucher.UsedAt = isPayOS ? null : DateTime.UtcNow;
                order.PromotionTemplateCode = voucher.TemplateCode;
                _context.PromotionVoucherEvents.Add(new PromotionVoucherEvent { VoucherId=voucher.Id, EventType=isPayOS ? VoucherEventTypes.Reserved : VoucherEventTypes.Used, UserName=currentUsername!, OrderId=order.Id, CreatedAt=DateTime.UtcNow });
                var reward = voucher.Reward ?? await _context.PromotionRewards.FirstOrDefaultAsync(item => item.TemplateCode == voucher.TemplateCode, cancellationToken);
                if (reward?.IsGift == true)
                {
                    _context.OrderGiftItems.Add(new OrderGiftItem { OrderId=order.Id, PromotionVoucherId=voucher.Id, Name=reward.GiftName ?? reward.Title, Quantity=1, UnitPrice=0 });
                }
            }

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
                        OrderCode = order.PayOSOrderCode!.Value,
                        Amount = decimal.ToInt32(order.TotalPrice),
                        Description = $"Don hang {order.Id}",
                        ReturnUrl = $"{baseUrl}/Payment/Return",
                        CancelUrl = $"{baseUrl}/Payment/Cancel"
                    };
                    var paymentLink = await payOS.PaymentRequests.CreateAsync(paymentRequest);
                    return Redirect(paymentLink.CheckoutUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PayOS không thể tạo link thanh toán cho đơn {OrderId}, orderCode {PayOSOrderCode}.", order.Id, order.PayOSOrderCode);
                    order.Status = OrderStatus.Cancelled;
                    foreach (var item in cartItems)
                    {
                        if (item.Product == null) continue;
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.Product.Id);
                        if (product != null) product.stockQuantity += item.Quantity;
                    }
                    var usedVoucher = await _context.PromotionVouchers.FirstOrDefaultAsync(voucher => voucher.OrderId == order.Id);
                    if (usedVoucher != null)
                    {
                        usedVoucher.UsedAt = null;
                        usedVoucher.OrderId = null;
                        _context.PromotionVoucherEvents.Add(new PromotionVoucherEvent { VoucherId=usedVoucher.Id, EventType=VoucherEventTypes.Released, UserName=currentUsername!, OrderId=order.Id, CreatedAt=DateTime.UtcNow, Note="Không tạo được liên kết PayOS" });
                    }
                    await _context.SaveChangesAsync();
                    TempData["CheckoutError"] = "Không thể tạo liên kết thanh toán PayOS. Vui lòng kiểm tra khóa cấu hình và thử lại.";
                    return RedirectToAction(nameof(Checkout));
                }
            }

            // Xóa sạch giỏ hàng sau khi đặt thành công
            HttpContext.Response.Cookies.Delete(CartSessionKey);
            HttpContext.Session.Remove(PromotionSessionKey);

            return RedirectToAction("OrderSuccess");
        }

        public IActionResult OrderSuccess()
        {
            return View();
        }
    }
}
