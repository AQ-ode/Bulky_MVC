using Bulky.DataAccess.Repository;
using Bulky.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CompanyController> _logger;

        public CompanyController(IUnitOfWork unitOfWork, ILogger<CompanyController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public IActionResult Index()
        {
            try
            {
                List<Company> objCompanyList = _unitOfWork.Company.GetAll().ToList();
                return View(objCompanyList);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching company list: {ex.Message}");
                return View(new List<Company>());
            }
        }

        public IActionResult Upsert(int? id)
        {
            try
            {
                if (id == null || id == 0)
                {
                    return View(new Company());
                }
                else
                {
                    Company companyObj = _unitOfWork.Company.Get(u => u.Id == id);
                    if (companyObj == null)
                    {
                        _logger.LogWarning($"Company with ID {id} not found.");
                        return NotFound();
                    }
                    //abc
                    return View(companyObj);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching company data: {ex.Message}");
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult Upsert(Company companyObj)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    if (companyObj.Id == 0)
                    {
                        _unitOfWork.Company.Add(companyObj);
                    }
                    else
                    {
                        _unitOfWork.Company.update(companyObj);
                    }
                    _unitOfWork.save();
                    return RedirectToAction("Index");
                }
                return View(companyObj);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving company data: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while saving data.");
                return View(companyObj);
            }
        }

        #region API CALLS
        [HttpGet]
        public IActionResult GetAll()
        {
            try
            {
                List<Company> companyObj = _unitOfWork.Company.GetAll().ToList();
                return Json(new { data = companyObj });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching companies: {ex.Message}");
                return Json(new { success = false, message = "Error fetching data." });
            }
        }

        [HttpDelete]
        public IActionResult Delete(int? id)
        {
            try
            {
                var companyToBeDeleted = _unitOfWork.Company.Get(u => u.Id == id);
                if (companyToBeDeleted == null)
                {
                    _logger.LogWarning($"Company with ID {id} not found.");
                    return Json(new { success = false, message = "Error while deleting Company" });
                }
                _unitOfWork.Company.Remove(companyToBeDeleted);
                _unitOfWork.save();
                return Json(new { success = true, message = "Company Deleted Successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting company: {ex.Message}");
                return Json(new { success = false, message = "Error deleting company." });
            }
        }
        #endregion
    }
}