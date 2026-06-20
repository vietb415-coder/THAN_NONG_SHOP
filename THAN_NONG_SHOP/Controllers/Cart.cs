using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

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

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private List<CartItem> GetCartItems()
        {
            var sessionData = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(sessionData))
            {
                return new List<CartItem>();
            }
            try
            {
                var cart = JsonSerializer.Deserialize<List<CartItem>>(sessionData, _jsonOptions);
                return cart ?? new List<CartItem>();
            }
            catch
            {
                return new List<CartItem>();
            }
        }

        private void SaveCartItems(List<CartItem> cartItems)
        {
            var sessionData = JsonSerializer.Serialize(cartItems);
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
            var cartItems = GetCartItems();
            if (!cartItems.Any())
            {
                return RedirectToAction("Index", "Home");
            }
            ViewBag.Total = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);
            return View();
        }
        [Authorize]
        [HttpPost]
        public IActionResult Checkout(Oder order)
        {
            var cartItems = GetCartItems();
            if (!cartItems.Any())
            {
                return RedirectToAction("Index", "Home");
            }

            order.OrderDate = DateTime.Now;
            order.TotalPrice = cartItems.Sum(item => (item.Product?.price ?? 0) * item.Quantity);
            order.Status = "Chờ xử lý";

            _context.Oders.Add(order);
            _context.SaveChanges();

            foreach (var item in cartItems)
            {
                if (item.Product != null)
                {
                    // Chú ý tên Class OderDetail (nếu em viết thiếu chữ r thì giữ nguyên)
                    var orderDetail = new OderDetail
                    {
                        OderId = order.Id,
                        ProductId = item.Product.Id,
                        Quantity = item.Quantity,
                        price = item.Product.price // decimal đồng bộ hoàn toàn
                    };
                    _context.OderDetails.Add(orderDetail);
                }
            }
            _context.SaveChanges();
            HttpContext.Session.Remove(CartSessionKey);

            return RedirectToAction(nameof(OrderSuccess));
        }

        public IActionResult OrderSuccess()
        {
            return View();
        }
    }
}