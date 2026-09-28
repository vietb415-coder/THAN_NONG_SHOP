using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Data;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using System.Data;

namespace THAN_NONG_SHOP.Controllers
{
    public class HomeController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _context;
        private readonly IDataProtector _voucherProtector;
        public HomeController(THAN_NONG_SHOP_DbContext context, IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _voucherProtector = dataProtectionProvider.CreateProtector("THAN_NONG_SHOP.VoucherCode.v1");
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            if (!await _context.Products.AsNoTracking().AnyAsync(product => product.Id == id, cancellationToken)) return NotFound();
            return RedirectToAction("Details", "Products", new { id });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Promotions()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SpinPromotion(CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "member";
            var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var existing = await _context.PromotionSpins.Include(spin => spin.Reward).Include(spin => spin.Voucher)
                .FirstOrDefaultAsync(spin => spin.UserName == userId && spin.SpinDate == today, cancellationToken);
            if (existing?.Reward != null && existing.Voucher != null)
            {
                await transaction.CommitAsync(cancellationToken);
                return Json(ToSpinResponse(existing.Reward, UnprotectCode(existing.Voucher.ProtectedCode), replay: true));
            }

            var eligible = await _context.PromotionRewards.Where(reward => reward.IsActive && reward.WheelWeight > 0 && (reward.StockRemaining == null || reward.StockRemaining > 0)).ToListAsync(cancellationToken);
            if (eligible.Count == 0) return Conflict(new { message = "Phần thưởng hôm nay đã được nhận hết. Vui lòng quay lại sau." });
            var totalWeight = eligible.Sum(reward => reward.WheelWeight);
            var draw = RandomNumberGenerator.GetInt32(totalWeight);
            var reward = eligible.First(item => (draw -= item.WheelWeight) < 0);
            var issued = await IssuePersonalVoucherAsync(userId, reward, cancellationToken);
            if (reward.StockRemaining.HasValue) reward.StockRemaining--;
            var spin = new PromotionSpin { UserName = userId, SpinDate = today, CreatedAt = DateTime.UtcNow, RewardId = reward.Id, Voucher = issued.Voucher };
            _context.PromotionSpins.Add(spin);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Json(ToSpinResponse(reward, issued.Code, replay: false));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClaimPromotion(string templateCode, CancellationToken cancellationToken)
        {
            templateCode = (templateCode ?? string.Empty).Trim().ToUpperInvariant();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var reward = await _context.PromotionRewards.FirstOrDefaultAsync(item => item.TemplateCode == templateCode && item.IsPublicOffer && item.IsActive, cancellationToken);
            if (reward == null) return BadRequest(new { message = "Ưu đãi không hợp lệ hoặc đã tạm dừng." });
            var issued = await IssuePersonalVoucherAsync(userId, reward, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Json(new { code = issued.Code });
        }

        private async Task<(PromotionVoucher Voucher, string Code)> IssuePersonalVoucherAsync(string userName, PromotionReward reward, CancellationToken cancellationToken)
        {
            // Thu hồi voucher chưa dùng cùng loại trước khi cấp mã mới; mỗi tài khoản
            // chỉ giữ một mã đang hoạt động cho từng ưu đãi.
            var oldVouchers = await _context.PromotionVouchers
                .Where(voucher => voucher.UserName == userName && voucher.TemplateCode == reward.TemplateCode && voucher.UsedAt == null && voucher.OrderId == null)
                .ToListAsync(cancellationToken);
            foreach (var oldVoucher in oldVouchers)
            {
                oldVoucher.UsedAt = DateTime.UtcNow;
                _context.PromotionVoucherEvents.Add(new PromotionVoucherEvent { VoucherId=oldVoucher.Id, EventType=VoucherEventTypes.Revoked, UserName=userName, CreatedAt=DateTime.UtcNow, Note="Thu hồi khi cấp mã mới cùng loại" });
            }

            string code;
            string hash;
            do
            {
                code = VoucherSecurity.GenerateCode();
                hash = VoucherSecurity.HashCode(code);
            }
            while (await _context.PromotionVouchers.AnyAsync(voucher => voucher.CodeHash == hash, cancellationToken));

            var voucher = new PromotionVoucher
            {
                CodeHash = hash,
                ProtectedCode = _voucherProtector.Protect(code),
                UserName = userName,
                TemplateCode = reward.TemplateCode,
                RewardId = reward.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = new DateTime(2026, 9, 30, 16, 59, 59, DateTimeKind.Utc)
            };
            _context.PromotionVouchers.Add(voucher);
            await _context.SaveChangesAsync(cancellationToken);
            _context.PromotionVoucherEvents.Add(new PromotionVoucherEvent { VoucherId=voucher.Id, EventType=VoucherEventTypes.Issued, UserName=userName, CreatedAt=DateTime.UtcNow });
            return (voucher, code);
        }

        private object ToSpinResponse(PromotionReward reward, string code, bool replay)
        {
            string[] order = ["LUCKY10","FREESHIP","QUA500K","LUCKY30K","THANNONG15","MATONG0D","MUAVANG50","DACBIET1TR"];
            var index = Array.IndexOf(order, reward.TemplateCode);
            return new { index = index < 0 ? 0 : index, title = reward.Title, description = reward.Description, code, replay };
        }

        private string UnprotectCode(string? value)
        {
            try { return string.IsNullOrWhiteSpace(value) ? "" : _voucherProtector.Unprotect(value); }
            catch { return ""; }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
