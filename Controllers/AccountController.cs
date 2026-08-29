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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;

namespace THAN_NONG_SHOP.Controllers
{
    public class AccountController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;
        private readonly IPasswordHasher<Models.user> _passwordHasher;
        private readonly IDataProtector _voucherProtector;

        public AccountController(THAN_NONG_SHOP_DbContext context, IPasswordHasher<Models.user> passwordHasher, IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _voucherProtector = dataProtectionProvider.CreateProtector("THAN_NONG_SHOP.VoucherCode.v1");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile(CancellationToken cancellationToken)
        {
            var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var account = await _context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.UserName == username, cancellationToken);
            if (account == null) return NotFound();

            var vouchers = await _context.PromotionVouchers
                .Where(item => item.UserName == username)
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
            var voucherOrderIds = vouchers.Where(item => item.OrderId.HasValue).Select(item => item.OrderId!.Value).Distinct().ToArray();
            var voucherOrderStatuses = await _context.Oders.AsNoTracking().Where(order => voucherOrderIds.Contains(order.Id))
                .ToDictionaryAsync(order => order.Id, order => order.Status, cancellationToken);
            // Sửa dữ liệu của các phiên bản cũ: trước đây PayOS vừa tạo link đã đánh dấu voucher là đã dùng.
            foreach (var voucher in vouchers.Where(item => item.OrderId.HasValue))
            {
                if (!voucherOrderStatuses.TryGetValue(voucher.OrderId!.Value, out var orderStatus) || orderStatus == Models.OrderStatus.Cancelled)
                {
                    voucher.OrderId = null;
                    voucher.UsedAt = null;
                }
                else if (orderStatus == Models.OrderStatus.AwaitingPayment)
                {
                    voucher.UsedAt = null;
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var voucherModels = vouchers.Select(voucher => new Models.AccountVoucherViewModel
            {
                Code = UnprotectVoucher(voucher.ProtectedCode),
                TemplateCode = voucher.TemplateCode,
                Benefit = GetVoucherBenefit(voucher.TemplateCode),
                Status = voucher.UsedAt != null ? "Đã sử dụng" : voucher.OrderId.HasValue ? "Đang giữ cho đơn" : voucher.ExpiresAt < now ? "Hết hạn" : "Có thể sử dụng",
                ExpiresAt = voucher.ExpiresAt,
                OrderId = voucher.OrderId
            }).ToList();
            var orders = await _context.Oders.AsNoTracking().Where(order => order.UserName == username)
                .OrderByDescending(order => order.OrderDate).Take(8).ToListAsync(cancellationToken);
            return View(new Models.AccountProfileViewModel { User = account, Vouchers = voucherModels, RecentOrders = orders });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName, string email, string phoneNumber)
        {
            var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var account = await _context.Users.FirstOrDefaultAsync(item => item.UserName == username);
            if (account == null) return NotFound();
            fullName = (fullName ?? "").Trim(); email = (email ?? "").Trim(); phoneNumber = (phoneNumber ?? "").Trim();
            if (fullName.Length is < 2 or > 100 || !MailAddress.TryCreate(email, out _) || !Regex.IsMatch(phoneNumber, @"^0\d{9}$"))
            {
                TempData["ProfileError"] = "Thông tin chưa hợp lệ. Hãy kiểm tra họ tên, email và số điện thoại 10 chữ số.";
                return RedirectToAction(nameof(Profile));
            }
            account.Fullname = fullName; account.Email = email; account.Phone = phoneNumber;
            await _context.SaveChangesAsync();
            TempData["ProfileSuccess"] = "Đã cập nhật thông tin tài khoản.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var account = await _context.Users.FirstOrDefaultAsync(item => item.UserName == username);
            if (account == null) return NotFound();
            var suppliedPassword = currentPassword ?? "";
            var verified = account.Password.StartsWith("AQAAAA", StringComparison.Ordinal)
                ? _passwordHasher.VerifyHashedPassword(account, account.Password, suppliedPassword) != PasswordVerificationResult.Failed
                : account.Password == suppliedPassword;
            if (!verified) TempData["ProfileError"] = "Mật khẩu hiện tại không chính xác.";
            else if ((newPassword ?? "").Length < 8) TempData["ProfileError"] = "Mật khẩu mới phải có ít nhất 8 ký tự.";
            else if (newPassword != confirmPassword) TempData["ProfileError"] = "Xác nhận mật khẩu mới không khớp.";
            else
            {
                account.Password = _passwordHasher.HashPassword(account, newPassword);
                await _context.SaveChangesAsync();
                TempData["ProfileSuccess"] = "Đã đổi mật khẩu thành công.";
            }
            return RedirectToAction(nameof(Profile));
        }

        private string UnprotectVoucher(string? protectedCode)
        {
            if (string.IsNullOrWhiteSpace(protectedCode)) return "Mã cũ không thể hiển thị";
            try { return _voucherProtector.Unprotect(protectedCode); }
            catch { return "Không thể giải mã"; }
        }

        private static string GetVoucherBenefit(string templateCode) => templateCode switch
        {
            "THANNONG15" => "Giảm 15% đơn từ 299K",
            "MUAVANG50" => "Giảm 50K đơn từ 499K",
            "FREESHIP" => "Miễn phí vận chuyển đơn từ 199K",
            "LUCKY10" => "Giảm 10% đơn từ 199K",
            "LUCKY30K" => "Giảm 30K đơn từ 299K",
            "MATONG0D" => "Tặng mật ong 0đ với đơn từ 499K",
            "QUA500K" => "Giỏ quà trị giá 500K",
            "DACBIET1TR" => "Giỏ quà đặc biệt trị giá 1 triệu",
            _ => "Ưu đãi thành viên"
        };

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe = false, string? returnUrl = null)
        {
            username = username?.Trim() ?? "";
            password ??= "";
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

            if (user != null && !user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
                return View();
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

                // Loại bỏ hoàn toàn phiên cũ trước khi tạo cookie cho tài khoản vừa đăng nhập.
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

             
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }
                else if (roleName == "Admin")
                {
                
                    return RedirectToAction("Index", "Product", new { area = "Admin" });
                }
                else
                {
                   
                    return RedirectToAction("Index", "Home");
                }
            }


            ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
            ViewData["LoginUsername"] = username;
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
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
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string username, string fullName, string email, string phoneNumber, string password, string? returnUrl = null)
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
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            var newUser = new Models.user
            {
                UserName = username,

                
                Fullname = fullName,

                Password = "",
                Email = email,
                Phone = phoneNumber, 
                RoleId = 2,
                IsActive = true
            };
            newUser.Password = _passwordHasher.HashPassword(newUser, password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            return RedirectToAction("Login", new { returnUrl });
        }
    }
}
