using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using System.Security.Claims;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _db;
        public AdminUsersController(THAN_NONG_SHOP_DbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _db.Users.Include(u => u.Role).ToListAsync();

            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, int roleId, bool isActive)
        {
            if (roleId is not (1 or 2))
            {
                return BadRequest("Quyền tài khoản không hợp lệ.");
            }

            var account = await _db.Users.FindAsync(id);
            if (account == null) return NotFound();

            var currentUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (account.UserName == currentUsername && (!isActive || roleId != 1))
            {
                TempData["UserError"] = "Bạn không thể khóa hoặc hạ quyền tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Index));
            }

            if (account.RoleId == 1 && account.IsActive && (!isActive || roleId != 1))
            {
                var activeAdminCount = await _db.Users.CountAsync(u => u.RoleId == 1 && u.IsActive);
                if (activeAdminCount <= 1)
                {
                    TempData["UserError"] = "Hệ thống phải còn ít nhất một tài khoản quản trị đang hoạt động.";
                    return RedirectToAction(nameof(Index));
                }
            }

            account.RoleId = roleId;
            account.IsActive = isActive;
            await _db.SaveChangesAsync();
            TempData["UserSuccess"] = "Đã cập nhật tài khoản.";
            return RedirectToAction(nameof(Index));
        }
       
    }
}
