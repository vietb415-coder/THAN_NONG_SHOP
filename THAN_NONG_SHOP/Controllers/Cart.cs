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
using Newtonsoft.Json;

namespace THAN_NONG_SHOP.Controllers
{
    public class CartController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;
        private const string CartSessionKey = "CartItems";

        public CartController(THAN_NONG_SHOP_DbContext context)
        {
            _context = context;
        }

        private readonly SystemTextJson.JsonSerializerOptions _jsonOptions = new SystemTextJson.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        private string currentUsername;

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

        private void SaveCartItems(List<CartItem> cartItems)
        {
            var cookieData = SystemTextJson.JsonSerializer.Serialize(cartItems);
            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(30), 
                HttpOnly = true,                   
                Secure = true                     
            };
            HttpContext.Response.Cookies.Append(CartSessionKey, cookieData, cookieOptions);
        }

        public IActionResult Index()
        {
            var cartItems = GetCartItems();
            ViewBag.Total = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);
            return View(cartItems);
        }

        [HttpPost]
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
                cartItems.Add(new CartItem { Product = product, Quantity = quantity });
            }
            else
            {
                existingItem.Quantity += quantity;
            }
            SaveCartItems(cartItems);
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
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

        [Authorize]
        [HttpGet]
        public IActionResult Checkout()
        {
            var catItems = GetCartItems();
            if (!catItems.Any())
            {
                return RedirectToAction("Index", "Home");
            }
            var currentUsername = User.Identity?.Name;
            var userProfile = _context.Users.FirstOrDefault(u => u.UserName == currentUsername);
            ViewBag.UserFullName = userProfile?.Fullname??"";
            ViewBag.UserPhone = userProfile?.Phone ?? "";
            ViewBag.Total = catItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Checkout(string shippingAddress, string shippingPhone)
        {
            var cartItems = GetCartItems();

            if (cartItems == null || cartItems.Count == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            var currentUsername = User.Identity?.Name;
            var userProfile = _context.Users.FirstOrDefault(u => u.UserName == currentUsername);

            if (userProfile == null)
            {
                return NotFound("Không tìm thấy thông tin tài khoản.");
            }

            var order = new Oder
            {
                OrderDate = DateTime.Now,
                UserName = currentUsername,
                CustomerName = userProfile.Fullname ?? "",
                Address = shippingAddress,
                PhoneNumber = shippingPhone,
                TotalPrice = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity),
                Status = "Đang chờ xử lý",
            };

            _context.Add(order);
            await _context.SaveChangesAsync();

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
            HttpContext.Response.Cookies.Delete(CartSessionKey);

            return RedirectToAction("OrderSuccess");
        }

        public IActionResult OrderSuccess()
        {
            return View("OrderSuccess");
        }
    }
}