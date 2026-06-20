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
            var sessionData = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(sessionData))
            {
                return new List<CartItem>();
            }
            try
            {
                
                var cart = SystemTextJson.JsonSerializer.Deserialize<List<CartItem>>(sessionData, _jsonOptions);
                return cart ?? new List<CartItem>();
            }
            catch
            {
                return new List<CartItem>();
            }
        }

        private void SaveCartItems(List<CartItem> cartItems)
        {
       
            var sessionData = SystemTextJson.JsonSerializer.Serialize(cartItems);
            HttpContext.Session.SetString(CartSessionKey, sessionData);
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
           
            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            var cartItems = string.IsNullOrEmpty(cartJson)
                ? new List<CartItem>()
                : JsonConvert.DeserializeObject<List<CartItem>>(cartJson);

            if (cartItems == null || cartItems.Count == 0)
            {
                return RedirectToAction("Index", "Home");
            }
            var currentUsername = User.Identity?.Name;
            var userProfile= _context.Users.FirstOrDefault(u => u.UserName == currentUsername);

            var order = new Oder
            {
                OrderDate = DateTime.Now,
                UserName = currentUsername,
                CustomerName = userProfile.Fullname ??"",
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

            HttpContext.Session.Remove(CartSessionKey);
            return RedirectToAction("OrderSuccess");
        }

        public IActionResult OrderSuccess()
        {
            return View("OrderSuccess");
        }
    }
}