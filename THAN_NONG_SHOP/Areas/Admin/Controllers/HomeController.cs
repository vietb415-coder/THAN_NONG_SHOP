using Microsoft.AspNetCore.Mvc;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
