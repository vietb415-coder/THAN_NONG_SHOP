using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Text.RegularExpressions;
using THAN_NONG_SHOP.Data;
using System.Linq;
using THAN_NONG_SHOP.Migrations;

namespace THAN_NONG_SHOP.Controllers
{
    public class AccountController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;
        private readonly IPasswordHasher<Models.user> _passwordHasher;

        public AccountController(THAN_NONG_SHOP_DbContext context, IPasswordHasher<Models.user> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe = false)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            var passwordIsValid = false;

            if (user != null)
            {
                // Hash do PasswordHasher tạo bắt đầu bằng marker AQAAAA.
                // Tài khoản cũ đang lưu plaintext sẽ được nâng cấp sau lần đăng nhập đúng đầu tiên.
                if (!user.Password.StartsWith("AQAAAA", StringComparison.Ordinal))
                {
                    passwordIsValid = user.Password == password;
                    if (passwordIsValid)
                    {
                        user.Password = _passwordHasher.HashPassword(user, password);
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    var result = _passwordHasher.VerifyHashedPassword(user, user.Password, password);
                    passwordIsValid = result != PasswordVerificationResult.Failed;
                    if (result == PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        user.Password = _passwordHasher.HashPassword(user, password);
                        await _context.SaveChangesAsync();
                    }
                }
            }

            if (user != null && passwordIsValid)
            {

                string roleName = "User";

               
                if (user.RoleId == 1)
                {
                    roleName = "Admin";
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserName),
                    new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.Fullname) ? user.UserName : user.Fullname),
                    new Claim(ClaimTypes.Role, roleName)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties { IsPersistent = rememberMe };

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
        [HttpGet]
        public IActionResult Register()
        {
           
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string username, string fullName, string email, string phoneNumber, string password)
        {
            username = username?.Trim() ?? "";
            fullName = fullName?.Trim() ?? "";
            email = email?.Trim() ?? "";
            phoneNumber = phoneNumber?.Trim() ?? "";
            password ??= "";

            if (username.Length < 3)
            {
                ModelState.AddModelError("username", "Tên đăng nhập phải có ít nhất 3 ký tự.");
            }
            else if (await _context.Users.AnyAsync(u => u.UserName == username))
            {
                ModelState.AddModelError("username", "Tên đăng nhập này đã tồn tại.");
            }

            if (fullName.Length < 2)
            {
                ModelState.AddModelError("fullName", "Vui lòng nhập họ và tên của bạn.");
            }

            if (!MailAddress.TryCreate(email, out _))
            {
                ModelState.AddModelError("email", "Địa chỉ email không hợp lệ.");
            }

            if (!Regex.IsMatch(phoneNumber, @"^0\d{9}$"))
            {
                ModelState.AddModelError("phoneNumber", "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.");
            }

            if (password.Length < 8)
            {
                ModelState.AddModelError("password", "Mật khẩu phải có ít nhất 8 ký tự.");
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            var newUser = new Models.user
            {
                UserName = username,

                
                Fullname = fullName,

                Password = "",
                Email = email,
                Phone = phoneNumber, 
                RoleId = 2
            };
            newUser.Password = _passwordHasher.HashPassword(newUser, password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            return RedirectToAction("Login");
        }
    }
}
