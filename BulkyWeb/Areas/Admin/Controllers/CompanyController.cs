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

        public CompanyController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

        }

        public IActionResult Index()
        {
            List<Company> objCompanyList = _unitOfWork.Company.GetAll().ToList();

            return View(objCompanyList);
        }
        public IActionResult Upsert(int? id)
        {


            if (id == null || id == 0)
            {

                return View(new Company());
            }
            else
            {
                Company companyObj = _unitOfWork.Company.Get(u => u.Id == id);
                return View(companyObj);
            }
        }

        [HttpPost]
        public IActionResult Upsert(Company companyObj)
        {
            if (ModelState.IsValid)
            {

                // Add or update the product
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
            else
            {
                // Reload the category list if model state is invalid

                return View(companyObj);
            }
        }



        #region API CALLS
        [HttpGet]
        public IActionResult GetAll()
        {

            List<Company> companyObj = _unitOfWork.Company.GetAll().ToList();
            return Json(new { data = companyObj });

        }

        public IActionResult Delete(int? id)
        {
            var companyToBeDeleted = _unitOfWork.Company.Get(u => u.Id == id);
            if (companyToBeDeleted == null)
            {
                return Json(new { success = false, message = "Error while deleting Company" });
            }

            _unitOfWork.Company.Remove(companyToBeDeleted);
            _unitOfWork.save();
            return Json(new { success = true, message = "Comapny Deleted Succesfully" });

        }
        #endregion
    }
}
