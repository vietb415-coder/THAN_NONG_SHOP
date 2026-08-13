using Microsoft.AspNetCore.Mvc;
using System.Linq;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly THAN_NONG_SHOP_DbContext _db;

        public CategoryController(THAN_NONG_SHOP_DbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            var categories = _db.Categories.ToList();
            return View(categories);
        }
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _db.Categories.Add(category);
                _db.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(category);


        }
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var category = _db.Categories.Find(id);
            if (category == null)
            {
                return NotFound();
            }
            var categories = _db.Categories.Find(id);
            if(categories == null)
            {
                return NotFound();
            }
            return View(category);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }
            if (ModelState.IsValid)
            {
                _db.Categories.Update(category);
                _db.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }
        [HttpGet]
        public IActionResult Delete(int id)
        {
            if (id == 0) return NotFound();
            var Categories = _db.Categories.Find(id);
            if (Categories == null) return NotFound();

            return View(Categories);
        }
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePOST(int id)
        {
            var categories = _db.Categories.Find(id);
            if (categories == null) { return NotFound(); }
            _db.Categories.Remove(categories);
            _db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

}
}
