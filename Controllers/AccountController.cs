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
using THAN_NONG_SHOP.Services;
using THAN_NONG_SHOP.Models;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;

namespace THAN_NONG_SHOP.Controllers
{
    public class AccountController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;
        private readonly IPasswordHasher<Models.user> _passwordHasher;
        private readonly IDataProtector _voucherProtector;
        private readonly EmailDelivery _email;
        private readonly CartState _cart;
        private readonly ILogger<AccountController>? _log;

        public AccountController(THAN_NONG_SHOP_DbContext context, IPasswordHasher<Models.user> passwordHasher, IDataProtectionProvider dataProtectionProvider, EmailDelivery email, CartState cart, ILogger<AccountController>? log = null)
        {
            _context = context;
            _email = email; _cart = cart;
            _passwordHasher = passwordHasher;
            _log = log;
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
            if (await _context.Users.AnyAsync(u => u.Id != account.Id && (u.NormalizedEmail == ShopRules.NormalizeEmail(email) || u.Phone == phoneNumber)))
            {
                TempData["ProfileError"] = "Email hoặc Số điện thoại đã được đăng ký";
                return RedirectToAction(nameof(Profile));
            }
            var changedEmail = !string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase);
            if (changedEmail && !_email.CanConfirm)
            {
                TempData["ProfileError"] = EmailConfiguration.UnavailableMessage;
                return RedirectToAction(nameof(Profile));
            }
            account.Fullname = fullName; account.Email = email; account.Phone = phoneNumber;
            if (changedEmail) { account.EmailConfirmed = false; _email.Confirmation(account); }
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException) { TempData["ProfileError"]="Email hoặc Số điện thoại đã được đăng ký"; return RedirectToAction(nameof(Profile)); }
            if (changedEmail) { await HttpContext.SignOutAsync(); TempData["AccountMessage"]="Vui lòng xác nhận địa chỉ email mới trước khi đăng nhập."; return RedirectToAction(nameof(Login)); }
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
            else if (!ShopRules.StrongPassword(newPassword)) TempData["ProfileError"] = "Mật khẩu phải có 8–128 ký tự, chữ hoa, chữ thường, số và ký tự đặc biệt.";
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
        [EnableRateLimiting("account")]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe = false, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            username = username?.Trim() ?? "";
            password ??= "";
            var normalized = ShopRules.NormalizeEmail(username);
            var matches = await _context.Users.Where(u => u.UserName == username || u.NormalizedEmail == normalized || u.Phone == username).Take(2).ToListAsync();
            var user = matches.Count == 1 ? matches[0] : null;
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

            if (user != null && passwordIsValid && !user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị tạm khóa. Vui lòng liên hệ Admin.");
                return View();
            }

            if (user != null && passwordIsValid && !user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty,"Vui lòng xác nhận email để kích hoạt tài khoản."); return View();
            }
            if (user != null && passwordIsValid)
            {

                string roleName = ShopRules.Role(user);

               
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
                await _cart.MergeGuestAsync(user.UserName);

             
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


            ModelState.AddModelError(string.Empty, "Thông tin đăng nhập không chính xác.");
            ViewData["LoginUsername"] = username;
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("CartItems");
            HttpContext.Session.Remove("AppliedPromotionCode");
            return RedirectToAction("Index", "Home");
        }


        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("account")]
        public async Task<IActionResult> Register(string username, string fullName, string email, string phoneNumber, string password, string confirmPassword, string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
            username = username?.Trim() ?? "";
            fullName = fullName?.Trim() ?? "";
            email = email?.Trim() ?? "";
            phoneNumber = phoneNumber?.Trim() ?? "";
            password ??= "";

            if (!Regex.IsMatch(username, @"^[A-Za-z][A-Za-z0-9_.-]{2,49}$"))
            {
                ModelState.AddModelError("username", "Tên đăng nhập dài 3–50 ký tự, bắt đầu bằng chữ, chỉ gồm chữ không dấu, số, dấu chấm, gạch ngang hoặc gạch dưới.");
            }

            if (fullName.Length is < 2 or > 100)
            {
                ModelState.AddModelError("fullName", "Vui lòng nhập họ và tên của bạn.");
            }

            if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsedEmail) || parsedEmail.Address != email)
            {
                ModelState.AddModelError("email", "Địa chỉ email không hợp lệ.");
            }

            if (!Regex.IsMatch(phoneNumber, @"^0\d{9}$"))
            {
                ModelState.AddModelError("phoneNumber", "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.");
            }

            if (!ShopRules.StrongPassword(password)) ModelState.AddModelError("password", "Mật khẩu phải có 8–128 ký tự, chữ hoa, chữ thường, số và ký tự đặc biệt.");
            if (password != confirmPassword) ModelState.AddModelError("confirmPassword", "Mật khẩu nhập lại không khớp.");
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View();

            // A retry while SMTP is sending must acknowledge the account already saved.
            if (await IsPendingRegistrationAsync(username, fullName, email, phoneNumber, password))
                return PendingRegistrationResult(returnUrl);

            if (await _context.Users.AnyAsync(u => u.UserName == username))
                ModelState.AddModelError("username", "Tên đăng nhập này đã tồn tại.");
            if (await _context.Users.AnyAsync(u => u.NormalizedEmail == ShopRules.NormalizeEmail(email) || u.Phone == phoneNumber))
                ModelState.AddModelError("email", "Email hoặc Số điện thoại đã được đăng ký");

            if (!ModelState.IsValid)
            {
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            if (!_email.CanConfirm)
            {
                ModelState.AddModelError("", EmailConfiguration.UnavailableMessage);
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
                IsActive = true, EmailConfirmed = false
            };
            newUser.Password = _passwordHasher.HashPassword(newUser, password);

            _context.Users.Add(newUser);
            var confirmation=_email.Confirmation(newUser);
            // Let this request attempt delivery first; worker retries if SMTP fails.
            confirmation.NextAttemptAt=DateTime.UtcNow.AddMinutes(1);
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException ex)
            {
                _context.Entry(newUser).State = EntityState.Detached;
                _context.Entry(confirmation).State = EntityState.Detached;
                if (ex.InnerException is SqlException sql && sql.Errors.Cast<SqlError>().Any(e => e.Number is 2601 or 2627))
                {
                    // Two requests can pass the initial checks before either commits.
                    if (await IsPendingRegistrationAsync(username, fullName, email, phoneNumber, password))
                        return PendingRegistrationResult(returnUrl);
                    ModelState.AddModelError("email", "Tên đăng nhập, Email hoặc Số điện thoại đã được đăng ký.");
                }
                else
                {
                    _log?.LogError(ex, "Không thể lưu tài khoản và email xác nhận khi đăng ký.");
                    ModelState.AddModelError("", "Không thể lưu đăng ký lúc này. Vui lòng thử lại sau hoặc liên hệ Admin.");
                }
                return View();
            }
            var sent=await _email.SendConfirmationNowAsync(confirmation);
            TempData["AccountMessage"]=sent
                ? "Đã tạo tài khoản thành công. Máy chủ email đã tiếp nhận thư xác nhận. Vui lòng mở liên kết trong hộp thư hoặc thư rác để kích hoạt tài khoản."
                : "Đã tạo tài khoản nhưng máy chủ email chưa gửi được thư. Hệ thống sẽ thử lại; bạn có thể bấm Gửi lại email xác nhận. Admin có thể xem lỗi tại mục Email.";
            return RedirectToAction("Login", new { returnUrl });
        }

        private async Task<bool> IsPendingRegistrationAsync(string username, string fullName, string email, string phone, string password)
        {
            var account = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == username);
            return account != null && !account.EmailConfirmed && account.IsActive && account.RoleId == 2
                && account.Fullname == fullName && account.Phone == phone
                && ShopRules.NormalizeEmail(account.Email) == ShopRules.NormalizeEmail(email)
                && account.Password.StartsWith("AQAAAA", StringComparison.Ordinal)
                && _passwordHasher.VerifyHashedPassword(account, account.Password, password) != PasswordVerificationResult.Failed;
        }

        private IActionResult PendingRegistrationResult(string? returnUrl)
        {
            TempData["AccountMessage"] = "Tài khoản đã được tạo và đang chờ xác nhận email. Vui lòng kiểm tra hộp thư và thư rác; nếu chưa nhận được thư, hãy bấm Gửi lại email xác nhận.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string username, string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length != 64) return BadRequest("Liên kết không hợp lệ.");
            var hash = EmailDelivery.Hash(token);
            var account = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username && u.ConfirmationTokenHash == hash && u.ConfirmationExpiresAt > DateTime.UtcNow);
            if (account == null) return BadRequest("Liên kết không hợp lệ, đã dùng hoặc đã hết hạn.");
            account.EmailConfirmed = true; account.ConfirmationTokenHash = null; account.ConfirmationExpiresAt = null;
            await _context.SaveChangesAsync();
            TempData["AccountMessage"]="Đã xác nhận email. Bạn có thể đăng nhập.";
            return RedirectToAction(nameof(Login));
        }
        [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("account")]
        public async Task<IActionResult> ResendConfirmation(string email)
        {
            if (!_email.CanConfirm)
            {
                TempData["AccountMessage"] = EmailConfiguration.UnavailableMessage;
                return RedirectToAction(nameof(Login));
            }
            var normalized = ShopRules.NormalizeEmail(email ?? "");
            var account = await _context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized && !u.EmailConfirmed);
            if (account != null && (account.ConfirmationExpiresAt == null || account.ConfirmationExpiresAt < DateTime.UtcNow.AddHours(24).AddMinutes(-5)))
            {
                var message=_email.Confirmation(account);message.NextAttemptAt=DateTime.UtcNow.AddMinutes(1);
                await _context.SaveChangesAsync();
                var sent=await _email.SendConfirmationNowAsync(message);
                TempData["AccountMessage"]=sent ? "Máy chủ email đã tiếp nhận thư xác nhận. Vui lòng kiểm tra hộp thư và thư rác."
                    : "Máy chủ email chưa gửi được thư xác nhận. Hệ thống sẽ thử lại; hãy liên hệ Admin nếu vẫn không nhận được.";
                return RedirectToAction(nameof(Login));
            }
            TempData["AccountMessage"]="Nếu email có tài khoản chưa kích hoạt và đã qua 5 phút từ yêu cầu trước, thư sẽ được đưa vào hàng đợi. Vui lòng kiểm tra hộp thư và thư rác.";
            return RedirectToAction(nameof(Login));
        }
        [Authorize(Roles="User"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestSeller()
        {
            var name=User.FindFirstValue(ClaimTypes.NameIdentifier);
            var account=await _context.Users.FirstAsync(u=>u.UserName==name);
            account.SellerRequested=true;
            await _context.SaveChangesAsync();
            TempData["ProfileSuccess"]="Đã gửi yêu cầu trở thành người bán. Vui lòng chờ Admin duyệt.";
            return RedirectToAction(nameof(Profile));
        }
    }
}
