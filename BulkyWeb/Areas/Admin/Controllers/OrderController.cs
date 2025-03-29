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
        private readonly ILogger<OrderController> _logger;

        [BindProperty]
        public OrderVm OrderVm { get; set; }
        public OrderController(IUnitOfWork unitOfWork, ILogger<OrderController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
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
            try
            {
                _logger.LogInformation("Updating order details for Order ID: {OrderId}", OrderVm.OrderHeader.Id);

                var orderHeaderFromDb = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVm.OrderHeader.Id);
                if (orderHeaderFromDb == null)
                {
                    _logger.LogWarning("Order ID {OrderId} not found", OrderVm.OrderHeader.Id);
                    return NotFound();
                }

                orderHeaderFromDb.Name = OrderVm.OrderHeader.Name;
                orderHeaderFromDb.PhoneNumber = OrderVm.OrderHeader.PhoneNumber;
                orderHeaderFromDb.StreetAddress = OrderVm.OrderHeader.StreetAddress;
                orderHeaderFromDb.City = OrderVm.OrderHeader.City;
                orderHeaderFromDb.State = OrderVm.OrderHeader.State;
                orderHeaderFromDb.PostalCode = OrderVm.OrderHeader.PostalCode;

                if (!string.IsNullOrEmpty(OrderVm.OrderHeader.Carrier))
                {
                    orderHeaderFromDb.Carrier = OrderVm.OrderHeader.Carrier;
                }
                if (!string.IsNullOrEmpty(OrderVm.OrderHeader.TrackingNumber))
                {
                    orderHeaderFromDb.TrackingNumber = OrderVm.OrderHeader.TrackingNumber;
                }

                _unitOfWork.OrderHeader.update(orderHeaderFromDb);
                _unitOfWork.save();

                _logger.LogInformation("Order details updated successfully for Order ID: {OrderId}", orderHeaderFromDb.Id);
                TempData["Success"] = "Order Details Have Been Saved Successfully";
                return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDb.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order details for Order ID: {OrderId}", OrderVm.OrderHeader.Id);
                TempData["Error"] = "An error occurred while updating order details.";
                return RedirectToAction(nameof(Details), new { orderId = OrderVm.OrderHeader.Id });
            }
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
            try
            {
                var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVm.OrderHeader.Id);
                if (orderHeader == null)
                {
                    TempData["Error"] = "Order not found.";
                    return RedirectToAction(nameof(Index));
                }

                orderHeader.TrackingNumber = OrderVm.OrderHeader.TrackingNumber;
                orderHeader.Carrier = OrderVm.OrderHeader.Carrier;
                orderHeader.OrderStatus = SD.StatusShipped;
                orderHeader.ShippingDate = DateTime.Now;

                if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
                {
                    orderHeader.PaymentDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30));
                }

                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusShipped);
                _unitOfWork.save();

                TempData["Success"] = "Order Details Have Been Updated Successfully";
                return RedirectToAction(nameof(Details), new { orderId = orderHeader.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error shipping order {OrderVm.OrderHeader?.Id}: {ex.Message}", ex);
                TempData["Error"] = "An error occurred while processing the order.";
                return RedirectToAction(nameof(Details), new { orderId = OrderVm.OrderHeader?.Id ?? 0 });
            }
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]

        public IActionResult CancelOrder()
        {
            try
            {
                _logger.LogInformation("Cancelling order ID: {OrderId}", OrderVm.OrderHeader.Id);

                var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVm.OrderHeader.Id);
                if (orderHeader == null)
                {
                    _logger.LogWarning("Order ID {OrderId} not found", OrderVm.OrderHeader.Id);
                    return NotFound();
                }

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
                _logger.LogInformation("Order ID {OrderId} has been cancelled successfully", orderHeader.Id);
                TempData["Success"] = "Order has been cancelled successfully";
                return RedirectToAction(nameof(Details), new { orderId = OrderVm.OrderHeader.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order ID: {OrderId}", OrderVm.OrderHeader.Id);
                TempData["Error"] = "An error occurred while cancelling the order.";
                return RedirectToAction(nameof(Details), new { orderId = OrderVm.OrderHeader.Id });
            }
        }

        [ActionName("Details")]
        [HttpPost]

        public IActionResult Details_Pay_Now()
        {
            try
            {
                _logger.LogInformation("Initiating payment process for Order ID: {OrderId}", OrderVm.OrderHeader.Id);

                OrderVm.OrderHeader = _unitOfWork.OrderHeader
                    .Get(u => u.Id == OrderVm.OrderHeader.Id, includeProperties: "ApplicationUser");
                OrderVm.OrderDetail = _unitOfWork.OrderDetail
                    .GetAll(u => u.OrderHeaderId == OrderVm.OrderHeader.Id, includeProperties: "Product");

                if (OrderVm.OrderHeader == null || !OrderVm.OrderDetail.Any())
                {
                    _logger.LogWarning("Order not found or order details are empty for Order ID: {OrderId}", OrderVm.OrderHeader.Id);
                    return NotFound();
                }

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

                if (session == null)
                {
                    _logger.LogError("Stripe session creation failed for Order ID: {OrderId}", OrderVm.OrderHeader.Id);
                    return StatusCode(500, "Error creating Stripe session.");
                }

                _logger.LogInformation("Stripe session created successfully. Session ID: {SessionId}, PaymentIntent: {PaymentIntentId}",
                    session.Id, session.PaymentIntentId);

                _unitOfWork.OrderHeader.UpdateStripePaymentId(OrderVm.OrderHeader.Id, session.Id, session.PaymentIntentId);
                _unitOfWork.save();

                _logger.LogInformation("Stripe Payment ID updated for Order ID: {OrderId}", OrderVm.OrderHeader.Id);

                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing payment for Order ID: {OrderId}", OrderVm.OrderHeader?.Id);
                return StatusCode(500, "An error occurred while processing the payment.");
            }
        }

        public IActionResult PaymentConfirmation(int orderHeaderid)
        {
            try
            {
                _logger.LogInformation("Checking payment confirmation for Order ID: {OrderId}", orderHeaderid);

                OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderHeaderid);
                if (orderHeader == null)
                {
                    _logger.LogWarning("Order not found for Order ID: {OrderId}", orderHeaderid);
                    return NotFound();
                }

                if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
                {
                    _logger.LogInformation("Fetching Stripe session for Order ID: {OrderId}, Session ID: {SessionId}",
                        orderHeaderid, orderHeader.SessionId);

                    var service = new SessionService();
                    Session session = service.Get(orderHeader.SessionId);

                    if (session == null)
                    {
                        _logger.LogError("Stripe session not found for Order ID: {OrderId}, Session ID: {SessionId}",
                            orderHeaderid, orderHeader.SessionId);
                        return StatusCode(500, "Error retrieving Stripe session.");
                    }

                    _logger.LogInformation("Stripe session retrieved. Payment Status: {PaymentStatus}", session.PaymentStatus);

                    if (session.PaymentStatus.ToLower() == "paid")
                    {
                        _logger.LogInformation("Payment confirmed for Order ID: {OrderId}. Updating database.", orderHeaderid);

                        _unitOfWork.OrderHeader.UpdateStripePaymentId(orderHeaderid, session.Id, session.PaymentIntentId);
                        _unitOfWork.OrderHeader.UpdateStatus(orderHeaderid, orderHeader.OrderStatus, SD.PaymentStatusApproved);
                        _unitOfWork.save();

                        _logger.LogInformation("Payment status updated successfully for Order ID: {OrderId}", orderHeaderid);
                    }
                    else
                    {
                        _logger.LogWarning("Payment not completed for Order ID: {OrderId}. Current status: {PaymentStatus}",
                            orderHeaderid, session.PaymentStatus);
                    }
                }
                else
                {
                    _logger.LogInformation("Order ID: {OrderId} is under delayed payment. No immediate payment update required.",
                        orderHeaderid);
                }

                return View(orderHeaderid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while confirming payment for Order ID: {OrderId}", orderHeaderid);
                return StatusCode(500, "An error occurred while confirming the payment.");
            }
        }

        #region API CALLS
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            try
            {
                _logger.LogInformation("Fetching all orders. Status filter: {Status}", status);

                IEnumerable<OrderHeader> objOrderHeaders;

                if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
                {
                    _logger.LogInformation("User is an Admin or Employee. Fetching all orders.");
                    objOrderHeaders = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser").ToList();
                }
                else
                {
                    var claimsIdentity = (ClaimsIdentity)User.Identity;
                    var userId = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    if (userId == null)
                    {
                        _logger.LogWarning("User ID not found in claims.");
                        return Unauthorized();
                    }

                    _logger.LogInformation("Fetching orders for user ID: {UserId}", userId);
                    objOrderHeaders = _unitOfWork.OrderHeader.GetAll(u => u.ApplicationUserId == userId, includeProperties: "ApplicationUser");
                }

                _logger.LogInformation("Applying status filter: {Status}", status);
                objOrderHeaders = status switch
                {
                    "pending" => objOrderHeaders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment),
                    "inprocess" => objOrderHeaders.Where(u => u.OrderStatus == SD.StatusInProgress),
                    "completed" => objOrderHeaders.Where(u => u.OrderStatus == SD.StatusShipped),
                    "approved" => objOrderHeaders.Where(u => u.OrderStatus == SD.StatusApproved),
                    _ => objOrderHeaders
                };

                _logger.LogInformation("Successfully fetched {Count} orders.", objOrderHeaders.Count());

                return Json(new { data = objOrderHeaders });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching orders. Status: {Status}", status);
                return StatusCode(500, new { error = "An error occurred while fetching the orders." });
            }
        }


        #endregion
    }
}
