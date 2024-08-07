using Bulky.DataAccess.Repository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        [BindProperty]
        public OrderVm OrderVm { get; set; }
        public OrderController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

        }

        public IActionResult Index()
        {
            return View();
        }


        public IActionResult Details(int orderId)
        {
            OrderVm orderVm = new()
            {
                OrderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderId, includeProperties: "ApplicationUser"),
                OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderId, includeProperties: "Product"),

            };
            return View(orderVm);
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult UpdateOrderDetail()
        {

            var OrderHeaderFromDb = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVm.OrderHeader.Id);
            OrderHeaderFromDb.Name = OrderVm.OrderHeader.Name;
            OrderHeaderFromDb.PhoneNumber = OrderVm.OrderHeader.PhoneNumber;
            OrderHeaderFromDb.StreetAddress = OrderVm.OrderHeader.StreetAddress;
            OrderHeaderFromDb.City = OrderVm.OrderHeader.City;
            OrderHeaderFromDb.State = OrderVm.OrderHeader.State;
            OrderHeaderFromDb.PostalCode = OrderVm.OrderHeader.PostalCode;
            if (!string.IsNullOrEmpty(OrderHeaderFromDb.Carrier))
            {
                OrderHeaderFromDb.Carrier = OrderHeaderFromDb.Carrier;
            }
            if (!string.IsNullOrEmpty(OrderHeaderFromDb.TrackingNumber))
            {
                OrderHeaderFromDb.TrackingNumber = OrderHeaderFromDb.TrackingNumber;
            }

            _unitOfWork.OrderHeader.update(OrderHeaderFromDb);
            _unitOfWork.save();
            TempData["Success"] = "Order Details Have Been Saved Succesfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderHeaderFromDb.Id });
        }
        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult Startprocessing()
        {
            _unitOfWork.OrderHeader.UpdateStatus(OrderVm.OrderHeader.Id, SD.StatusInProgress);
            _unitOfWork.save();
            TempData["Success"] = "Order Details Have Been Updated Succesfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVm.OrderHeader.Id });
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult ShipOrder()
        {
            var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVm.OrderHeader.Id);
            orderHeader.TrackingNumber = OrderVm.OrderHeader.TrackingNumber;
            orderHeader.Carrier = OrderVm.OrderHeader.Carrier;
            orderHeader.OrderStatus = SD.StatusShipped;
            orderHeader.ShippingDate = DateTime.Now;
            if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                orderHeader.PaymentDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30));
            }
            _unitOfWork.OrderHeader.UpdateStatus(OrderVm.OrderHeader.Id, SD.StatusShipped);
            _unitOfWork.save();
            TempData["Success"] = "Order Details Have Been Updated Succesfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVm.OrderHeader.Id });
        }
        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult CancelOrder()
        {
            var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVm.OrderHeader.Id);
            if (orderHeader.PaymentStatus == SD.PaymentStatusApproved)
            {
                var options = new RefundCreateOptions
                {
                    Reason = RefundReasons.RequestedByCustomer,
                    PaymentIntent = orderHeader.PaymentIntentId
                };
                var service = new RefundService();
                Refund refund = service.Create(options);
                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusRefunded);
            }
            else
            {
                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);
            }
            _unitOfWork.save();
            TempData["Success"] = "Order Details Have Been Cancelled Succesfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVm.OrderHeader.Id });
        }
        [ActionName("Details")]
        [HttpPost]

        public IActionResult Details_Pay_Now()
        {
            OrderVm.OrderHeader = _unitOfWork.OrderHeader
                .Get(u => u.Id == OrderVm.OrderHeader.Id, includeProperties: "ApplicationUser");
            OrderVm.OrderDetail = _unitOfWork.OrderDetail
               .GetAll(u => u.OrderHeaderId == OrderVm.OrderHeader.Id, includeProperties: "Product");
            var domain = "https://localhost:7293/";

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                SuccessUrl = domain + $"admin/order/PaymentConfirmation?orderHeaderId={OrderVm.OrderHeader.Id}",
                CancelUrl = domain + $"admin/order/details?orderId={OrderVm.OrderHeader.Id}",
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
                Mode = "payment",
            };
            foreach (var item in OrderVm.OrderDetail)
            {
                var sessionLineItem = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)item.Price * 100,
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Title
                        }
                    },
                    Quantity = item.Count
                };
                options.LineItems.Add(sessionLineItem);
            }
            var service = new SessionService();
            Session session = service.Create(options);
            _unitOfWork.OrderHeader.UpdateStripePaymentId(OrderVm.OrderHeader.Id, session.Id, session.PaymentIntentId);
            _unitOfWork.save();
            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);


        }
        public IActionResult PaymentConfirmation(int orderHeaderid)
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderHeaderid);
            if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
                var service = new SessionService();
                Session session = service.Get(orderHeader.SessionId);
                if (session.PaymentStatus.ToLower() == "paid")
                {

                    _unitOfWork.OrderHeader.UpdateStripePaymentId(orderHeaderid, session.Id, session.PaymentIntentId);
                    _unitOfWork.OrderHeader.UpdateStatus(orderHeaderid, orderHeader.OrderStatus, SD.PaymentStatusApproved);
                    _unitOfWork.save();
                }

            }

            return View(orderHeaderid);
        }
        #region API CALLS
        [HttpGet]
        public IActionResult GetAll(string status)
        {

            IEnumerable<OrderHeader> objOrderHeaders = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser").ToList();
            if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
            {
                objOrderHeaders = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser").ToList();

            }
            else
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
                objOrderHeaders = _unitOfWork.OrderHeader.GetAll(u => u.ApplicationUserId == userId, includeProperties: "ApplicationUser");
            }
            switch (status)
            {
                case "pending":
                    objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
                    break;
                case "inprocess":
                    objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusInProgress);
                    break;
                case "completed":
                    objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusShipped);
                    break;
                case "approved":
                    objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusApproved);
                    break;
                default:
                    break;
            }


            return Json(new { data = objOrderHeaders });

        }

        #endregion
    }
}
