using Microsoft.AspNetCore.Mvc;
using Resturant.Core.Interfaces;
using Resturant.Core.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Resturant.Web.UI.Hubs;

namespace Resturant.Web.UI.Controllers
{
    public class CustomerSessionController : Controller
    {
        private readonly ISessionService _sessionService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHubContext<OrderHub> _hubContext;

        public CustomerSessionController(ISessionService sessionService, IUnitOfWork unitOfWork, IHubContext<OrderHub> hubContext)
        {
            _sessionService = sessionService;
            _unitOfWork = unitOfWork;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Scan(int tableNumber)
        {
            // Cache table number for 12 hours in a cookie (HttpOnly so server-only)
            CookieOptions option = new CookieOptions
            {
                Expires = DateTime.Now.AddHours(12),
                HttpOnly = true,
                Secure = true
            };
            Response.Cookies.Append("TableNumber", tableNumber.ToString(), option);

            var tables = await _unitOfWork.Repository<RestaurantTable>().ListAsync(t => t.TableNumber == tableNumber);
            var table = tables.FirstOrDefault();
            
            if (table == null) return NotFound("Table not found");

            // Only auto-allow if the table is OCCUPIED and the current device's server cookies
            // exactly match the person who currently holds the session.
            // For FREE tables we ALWAYS want to show the form (even if cookies/localStorage exist).
            string guestName = Request.Cookies["GuestName"];
            string guestPhone = Request.Cookies["GuestPhone"];

            var activeSession = await _sessionService.GetActiveSessionByTableAsync(table.Id);
            
            if (activeSession != null)
            {
                // Table has an active reservation.
                // If the visitor's cookies prove they are the original guest → let them in directly.
                if (!string.IsNullOrEmpty(guestName) && !string.IsNullOrEmpty(guestPhone) &&
                    string.Equals(activeSession.CustomerName, guestName, StringComparison.OrdinalIgnoreCase) && 
                    activeSession.PhoneNumber == guestPhone)
                {
                    return RedirectToAction("Index", "Menu");
                }
            }

            // Free table OR occupied but no matching proof on this device:
            // → Always show the registration / verification form.
            // The form (Register.cshtml) will pre-fill from localStorage (client cache on phone)
            // but will NOT auto-submit, so the user always sees the form.
            return RedirectToAction("Register");
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            string tableNumberStr = Request.Cookies["TableNumber"];
            if (string.IsNullOrEmpty(tableNumberStr))
            {
                return RedirectToAction("Index", "Home");
            }

            if (int.TryParse(tableNumberStr, out int tableNumber))
            {
                var tables = await _unitOfWork.Repository<RestaurantTable>().ListAsync(t => t.TableNumber == tableNumber);
                var table = tables.FirstOrDefault();
                if (table != null)
                {
                    var active = await _sessionService.GetActiveSessionByTableAsync(table.Id);
                    if (active != null)
                    {
                        ViewBag.TableIsOccupied = true;
                        ViewBag.OccupantHint = "This table is already reserved. Please enter the exact name and phone number used when it was first scanned.";
                    }
                }
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string customerName, string phoneNumber)
        {
            if (string.IsNullOrEmpty(customerName) || string.IsNullOrEmpty(phoneNumber))
            {
                ViewBag.Error = "Name and Phone Number are required.";
                return View();
            }

            string tableNumberStr = Request.Cookies["TableNumber"];
            if (string.IsNullOrEmpty(tableNumberStr))
            {
                return RedirectToAction("Index", "Home"); // Should ideally show an error page
            }

            if (!int.TryParse(tableNumberStr, out int tableNumber))
            {
                 return RedirectToAction("Index", "Home");
            }

            var tables = await _unitOfWork.Repository<RestaurantTable>().ListAsync(t => t.TableNumber == tableNumber);
            var table = tables.FirstOrDefault();
            
            var activeSession = await _sessionService.GetActiveSessionByTableAsync(table.Id);
            
            if (activeSession != null)
            {
                // Verify if the guest is the one who created the session
                if (string.Equals(activeSession.CustomerName, customerName, StringComparison.OrdinalIgnoreCase) && 
                    activeSession.PhoneNumber == phoneNumber)
                {
                    // Identity verified! Set cookies and proceed.
                    SetGuestCookies(customerName, phoneNumber);
                    return RedirectToAction("Index", "Menu");
                }
                else
                {
                    ViewBag.Error = "This table is currently occupied by another guest. Please verify your details or contact staff.";
                    return View();
                }
            }

            // Table is free, start new session
            await _sessionService.StartSessionAsync(table.Id, customerName, phoneNumber);
            SetGuestCookies(customerName, phoneNumber);

            // === REAL-TIME UPDATE: Notify Waiter (and Admin) that a new guest has seated ===
            // This makes the Waiter page refresh its table list automatically without manual refresh.
            var notifyPayload = new { tableNumber = tableNumber, customerName = customerName, eventType = "GuestSeated" };
            await _hubContext.Clients.Group("waiter").SendAsync("ReceiveWaiterUpdate", notifyPayload);
            await _hubContext.Clients.Group("admin").SendAsync("ReceiveWaiterUpdate", notifyPayload);
            // Also broadcast a generic order-status change so other dashboards can react if needed
            await _hubContext.Clients.All.SendAsync("OrderStatusChanged", notifyPayload);

            return RedirectToAction("Index", "Menu");
        }

        private void SetGuestCookies(string name, string phone)
        {
            CookieOptions options = new CookieOptions
            {
                Expires = DateTime.Now.AddHours(12),
                HttpOnly = true,
                Secure = true
            };
            Response.Cookies.Append("GuestName", name, options);
            Response.Cookies.Append("GuestPhone", phone, options);
        }
    }
}
