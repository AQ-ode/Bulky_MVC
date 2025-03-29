using Bulky.DataAccess.Repository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using System.Security.Claims;

namespace BulkyWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CartController> _logger;
        public CartController(IUnitOfWork unitOfWork, ILogger<CartController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }
        public IActionResult Index()
        {
            _logger.LogInformation("Cart Index page accessed.");
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product"),
                OrderHeader = new()
            };

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            _logger.LogInformation("Cart loaded successfully for user {UserId}.", userId);
            return View(ShoppingCartVM);
        }
        public IActionResult Summary()
        {
            try
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
                ShoppingCartVM = new()
                {

                    ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties
                    : "Product"),
                    OrderHeader = new()
                };
                ShoppingCartVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
                ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
                ShoppingCartVM.OrderHeader.StreetAddress = ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
                ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
                ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
                ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;

                foreach (var cart in ShoppingCartVM.ShoppingCartList)
                {
                    cart.Price = GetPriceBasedOnQuantity(cart);
                    ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
                }
                return View(ShoppingCartVM);
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error in Summary method");
                return RedirectToAction("Error", "Home");

            }
        }
        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPost()
        {
            try
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("User ID is null or empty in SummaryPost.");
                    return RedirectToAction("Error", "Home");
                }

                ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product");
                ShoppingCartVM.OrderHeader.OrderDate = DateTime.UtcNow;
                ShoppingCartVM.OrderHeader.ApplicationUserId = userId;
                ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);

                foreach (var cart in ShoppingCartVM.ShoppingCartList)
                {
                    cart.Price = GetPriceBasedOnQuantity(cart);
                    ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
                }

                if (applicationUser.CompanyId.GetValueOrDefault() == 0)
                {
                    ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
                    ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
                }
                else
                {
                    ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
                    ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
                }

                _unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);

                // Validation checks
                if (string.IsNullOrWhiteSpace(ShoppingCartVM.OrderHeader.Name))
                {
                    ModelState.AddModelError("OrderHeader.Name", "Customer name cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(ShoppingCartVM.OrderHeader.PhoneNumber))
                {
                    ModelState.AddModelError("OrderHeader.PhoneNumber", "Phone number cannot be empty.");
                }

                if (ModelState.ContainsKey("OrderHeader.Name") && ModelState["OrderHeader.Name"].Errors.Count > 0 ||
               ModelState.ContainsKey("OrderHeader.PhoneNumber") && ModelState["OrderHeader.PhoneNumber"].Errors.Count > 0)
                {
                    return View("Summary", ShoppingCartVM);
                }

                _unitOfWork.save();

                foreach (var cart in ShoppingCartVM.ShoppingCartList)
                {
                    try
                    {
                        OrderDetail orderDetail = new()
                        {
                            ProductId = cart.ProductId,
                            OrderHeaderId = ShoppingCartVM.OrderHeader.Id,
                            Price = cart.Price,
                            Count = cart.Count
                        };

                        _unitOfWork.OrderDetail.Add(orderDetail);
                        _unitOfWork.save();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while adding order details in SummaryPost.");
                        return RedirectToAction("Error", "Home");
                    }
                }

                if (applicationUser.CompanyId.GetValueOrDefault() == 0)
                {
                    try
                    {
                        var domain = "https://localhost:7293/";

                        var options = new Stripe.Checkout.SessionCreateOptions
                        {
                            SuccessUrl = domain + $"customer/cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}",
                            CancelUrl = domain + "customer/cart/index",
                            LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
                            Mode = "payment",
                        };

                        foreach (var item in ShoppingCartVM.ShoppingCartList)
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

                        _unitOfWork.OrderHeader.UpdateStripePaymentId(ShoppingCartVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
                        _unitOfWork.save();

                        Response.Headers.Add("Location", session.Url);
                        return new StatusCodeResult(303);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while processing Stripe payment in SummaryPost.");
                        return RedirectToAction("Error", "Home");
                    }
                }

                return RedirectToAction(nameof(OrderConfirmation), new { id = ShoppingCartVM.OrderHeader.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in SummaryPost.");
                return RedirectToAction("Error", "Home");
            }
        }

        public IActionResult OrderConfirmation(int id)
        {
            try
            {
                OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id, includeProperties: "ApplicationUser");
                if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
                {
                    var service = new SessionService();
                    Session session = service.Get(orderHeader.SessionId);
                    if (session.PaymentStatus.ToLower() == "paid")
                    {

                        _unitOfWork.OrderHeader.UpdateStripePaymentId(id, session.Id, session.PaymentIntentId);
                        _unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                        _unitOfWork.save();
                    }

                    HttpContext.Session.Clear();

                }
                List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart.
                    GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
                _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
                _unitOfWork.save();

                return View(id);
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrderConfirmation method");
                return RedirectToAction("Error", "Home");
            }
        }
        public IActionResult Plus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);
            cartFromDb.Count += 1;
            _unitOfWork.ShoppingCart.update(cartFromDb);
            _unitOfWork.save();
            return RedirectToAction(nameof(Index));
        }
        public IActionResult Minus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true);
            if (cartFromDb.Count <= 1)
            {
                HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.
                    GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).
                    Count() - 1);

                _unitOfWork.ShoppingCart.Remove(cartFromDb);
            }
            else
            {
                cartFromDb.Count -= 1;
                _unitOfWork.ShoppingCart.update(cartFromDb);
            }

            _unitOfWork.save();
            return RedirectToAction(nameof(Index));
        }
        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true);
            HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.
                GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).
                Count() - 1);
            _unitOfWork.ShoppingCart.Remove(cartFromDb);
            _unitOfWork.save();
            return RedirectToAction(nameof(Index));
        }
        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else if (shoppingCart.Count <= 100)
            {
                return shoppingCart.Product.Price100;
            }
            else
            {
                return shoppingCart.Product.Price50;
            }
        }
    }
}

