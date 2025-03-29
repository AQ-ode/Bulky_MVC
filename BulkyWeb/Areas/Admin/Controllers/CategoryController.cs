using Bulky.DataAccess.Repository;
using Bulky.Models;
using Microsoft.AspNetCore.Mvc;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    //[Authorize(Roles = SD.Role_Admin)]
    public class CategoryController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CategoryController> _logger;
        public CategoryController(IUnitOfWork unitOfWork, ILogger<CategoryController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }
        public IActionResult Index()
        {
            try
            {
                List<Category> objCategoryList = _unitOfWork.Category.GetAll().ToList();
                return View(objCategoryList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching category list.");
                return RedirectToAction("Error", "Home");
            }
        }
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Route("Admin/Category/Create")]
        public IActionResult Create([FromBody] Category category)
        {
            try
            {
                if (category.Name == category.DisplayOrder.ToString())
                {
                    ModelState.AddModelError("name", "Display Order cannot exactly match the name");
                }
                if (ModelState.IsValid)
                {
                    _unitOfWork.Category.Add(category);
                    _unitOfWork.save();
                    return RedirectToAction("Index");
                }
                return View(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category.");
                return RedirectToAction("Error", "Home");
            }
        }
        public IActionResult Edit(int? id)
        {
            try
            {
                if (id == null || id == 0)
                {
                    return NotFound();
                }
                Category categoryFromDb = _unitOfWork.Category.Get(u => u.Id == id);
                if (categoryFromDb == null)
                {
                    return NotFound();
                }
                return View(categoryFromDb);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching category for edit.");
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpPost]
        public IActionResult Edit(Category category)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _unitOfWork.Category.update(category);
                    _unitOfWork.save();
                    return RedirectToAction("Index");
                }
                return View(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category.");
                return RedirectToAction("Error", "Home");
            }
        }
        public IActionResult Delete(int? id)
        {
            try
            {
                if (id == null || id == 0)
                {
                    return NotFound();
                }
                Category categoryFromDb = _unitOfWork.Category.Get(u => u.Id == id);
                if (categoryFromDb == null)
                {
                    return NotFound();
                }
                return View(categoryFromDb);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching category for delete.");
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeletePost(int? id)
        {
            try
            {
                Category? obj = _unitOfWork.Category.Get(u => u.Id == id);
                if (obj == null)
                {
                    return NotFound();
                }
                _unitOfWork.Category.Remove(obj);
                _unitOfWork.save();
                TempData["success"] = "Data Deleted Successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category.");
                return RedirectToAction("Error", "Home");
            }
        }
    }
}
