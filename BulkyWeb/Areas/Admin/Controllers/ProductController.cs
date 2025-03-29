using Bulky.DataAccess.Repository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [EnableCors("AllowSpecificOrigin")]
    [Area("Admin")]
    //[Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<ProductController> _logger;

        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, ILogger<ProductController> logger)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        public IActionResult Index()
        {
            _logger.LogInformation("Fetching all products.");
            List<Product> objProductList = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
            return View(objProductList);
        }

        public IActionResult Upsert(int? id)
        {
            _logger.LogInformation("Upsert method called for Product ID: {ProductId}", id);

            ProductVm productVM = new()
            {
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }),
                product = new Product()
            };

            if (id == null || id == 0)
            {
                return View(productVM);
            }

            productVM.product = _unitOfWork.Product.Get(u => u.Id == id);
            if (productVM.product == null)
            {
                _logger.LogWarning("Product not found with ID: {ProductId}", id);
                return NotFound();
            }

            return View(productVM);
        }

        [HttpPost]
        public IActionResult Upsert(ProductVm productVM, IFormFile? file)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid for product upsert.");
                productVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                });
                return View(productVM);
            }

            try
            {
                string wwwRootPath = _webHostEnvironment.WebRootPath;
                if (file != null)
                {
                    string filename = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    string productPath = Path.Combine(wwwRootPath, "images", "product");

                    // Delete old image if exists
                    if (!string.IsNullOrEmpty(productVM.product.ImageUrl))
                    {
                        var oldImagePath = Path.Combine(wwwRootPath, productVM.product.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                            _logger.LogInformation("Deleted old image for Product ID: {ProductId}", productVM.product.Id);
                        }
                    }

                    if (!Directory.Exists(productPath))
                    {
                        Directory.CreateDirectory(productPath);
                    }

                    using (var fileStream = new FileStream(Path.Combine(productPath, filename), FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }

                    productVM.product.ImageUrl = "/images/product/" + filename;
                    _logger.LogInformation("New image uploaded for Product ID: {ProductId}", productVM.product.Id);
                }

                if (productVM.product.Id == 0)
                {
                    _unitOfWork.Product.Add(productVM.product);
                    _logger.LogInformation("New product added.");
                }
                else
                {
                    _unitOfWork.Product.update(productVM.product);
                    _logger.LogInformation("Existing product updated, ID: {ProductId}", productVM.product.Id);
                }

                _unitOfWork.save();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while upserting product.");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        #region API CALLS

        [HttpGet]
        public IActionResult GetAll()
        {
            _logger.LogInformation("Fetching all products via API.");
            List<Product> objProductList = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
            return Json(new { data = objProductList });
        }

        [HttpDelete]
        public IActionResult Delete(int? id)
        {
            if (id == null || id == 0)
            {
                _logger.LogWarning("Invalid Product ID passed for deletion.");
                return Json(new { success = false, message = "Invalid product ID." });
            }

            var productToBeDeleted = _unitOfWork.Product.Get(u => u.Id == id);
            if (productToBeDeleted == null)
            {
                _logger.LogWarning("Product not found with ID: {ProductId}", id);
                return Json(new { success = false, message = "Product not found." });
            }

            try
            {
                var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, productToBeDeleted.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                    _logger.LogInformation("Deleted image for Product ID: {ProductId}", id);
                }

                _unitOfWork.Product.Remove(productToBeDeleted);
                _unitOfWork.save();
                _logger.LogInformation("Product deleted successfully, ID: {ProductId}", id);
                return Json(new { success = true, message = "Product Deleted Successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting product ID: {ProductId}", id);
                return Json(new { success = false, message = "Error while deleting product." });
            }
        }

        #endregion
    }
}
