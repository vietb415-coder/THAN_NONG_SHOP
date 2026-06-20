using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using THAN_NONG_SHOP.Data;
using System.Linq;

namespace THAN_NONG_SHOP.Controllers
{
    public class AccountController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;

        public AccountController(THAN_NONG_SHOP_DbContext context)
        {
            _context = context;
        }

        // 1. Hiển thị trang giao diện đăng nhập
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
        
            var user = _context.Users.FirstOrDefault(u => u.Fullname == username && u.Password == password);

            if (user != null)
            {

                string roleName = "User";

               
                if (user.RoleId == 1)
                {
                    roleName = "Admin";
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Fullname),
                    new Claim(ClaimTypes.Role, roleName) // Đóng dấu quyền vào thẻ bài Cookie
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties { IsPersistent = true };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

             
                if (roleName == "Admin")
                {
                
                    return RedirectToAction("Index", "Product", new { area = "Admin" });
                }
                else
                {
                   
                    return RedirectToAction("Index", "Home");
                }
            }


            ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
            return View();
        }


        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }


        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}