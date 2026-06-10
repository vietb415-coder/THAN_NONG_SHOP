using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Controllers
{
  
    public class AccountController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;
        
        public AccountController(THAN_NONG_SHOP_DbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Register (user Model)
        {
            if(ModelState.IsValid)
            {
            bool isEmailExist = _context.Users.Any(u => u.Email == Model.Email);
            if(isEmailExist)
            {
                ModelState.AddModelError("Email", "Email đã tồn tại");
                    return View(Model);
                }
            _context.Users.Add(Model);
                _context.SaveChanges();
                return RedirectToAction("Login");
            }
            return View(Model);
        }
       [HttpGet]
       public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email && u.Password == password);
            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("UserEmail", user.Email);
                return RedirectToAction("Index", "Home");
            }
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng");
            return View();

        }

        public IActionResult logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
