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
        public async Task<IActionResult> Scan(int branchId, int tableNumber)
        {
            // Cache table number and branch ID for 12 hours in cookies
            CookieOptions option = new CookieOptions
            {
                Expires = DateTime.Now.AddHours(12),
                HttpOnly = true,
                Secure = true
            };
            Response.Cookies.Append("TableNumber", tableNumber.ToString(), option);
            Response.Cookies.Append("BranchId", branchId.ToString(), option);

            var tables = await _unitOfWork.Repository<RestaurantTable>().ListAsync(t => t.TableNumber == tableNumber && t.BranchId == branchId);
            var table = tables.FirstOrDefault();
            
            if (table == null) return NotFound("Table not found");

            // Only auto-allow if the table is OCCUPIED and the current device's server cookies
            // exactly match the person who currently holds the session.
            string guestName = Request.Cookies["GuestName"];
            string guestPhone = Request.Cookies["GuestPhone"];

            var activeSession = await _sessionService.GetActiveSessionByTableAsync(table.Id);
            
            if (activeSession != null)
            {
                // Table has an active reservation.
                if (!string.IsNullOrEmpty(guestName) && !string.IsNullOrEmpty(guestPhone) &&
                    string.Equals(activeSession.CustomerName, guestName, StringComparison.OrdinalIgnoreCase) && 
                    activeSession.PhoneNumber == guestPhone)
                {
                    return RedirectToAction("Index", "Menu");
                }
            }

            return RedirectToAction("Register");
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            string tableNumberStr = Request.Cookies["TableNumber"];
            string branchIdStr = Request.Cookies["BranchId"];
            if (string.IsNullOrEmpty(tableNumberStr) || string.IsNullOrEmpty(branchIdStr))
            {
                return RedirectToAction("Index", "Home");
            }

            if (int.TryParse(tableNumberStr, out int tableNumber) && int.TryParse(branchIdStr, out int branchId))
            {
                var tables = await _unitOfWork.Repository<RestaurantTable>().ListAsync(t => t.TableNumber == tableNumber && t.BranchId == branchId);
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
            string branchIdStr = Request.Cookies["BranchId"];
            if (string.IsNullOrEmpty(tableNumberStr) || string.IsNullOrEmpty(branchIdStr))
            {
                return RedirectToAction("Index", "Home");
            }

            if (!int.TryParse(tableNumberStr, out int tableNumber) || !int.TryParse(branchIdStr, out int branchId))
            {
                 return RedirectToAction("Index", "Home");
            }

            var tables = await _unitOfWork.Repository<RestaurantTable>().ListAsync(t => t.TableNumber == tableNumber && t.BranchId == branchId);
            var table = tables.FirstOrDefault();
            if (table == null)
            {
                return NotFound("Table not found");
            }
            
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
            var notifyPayload = new { branchId = branchId, tableNumber = tableNumber, customerName = customerName, eventType = "GuestSeated" };
            await _hubContext.Clients.Group($"waiter_{branchId}").SendAsync("ReceiveWaiterUpdate", notifyPayload);
            await _hubContext.Clients.Group($"admin_{branchId}").SendAsync("ReceiveWaiterUpdate", notifyPayload);
            await _hubContext.Clients.Group($"guest_{branchId}").SendAsync("OrderStatusChanged", notifyPayload);

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
