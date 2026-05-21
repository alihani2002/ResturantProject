using Microsoft.AspNetCore.Mvc;
using Resturant.Core.Interfaces;
using Resturant.Core.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Resturant.Web.UI.Controllers
{
    public class CustomerSessionController : Controller
    {
        private readonly ISessionService _sessionService;
        private readonly IUnitOfWork _unitOfWork;

        public CustomerSessionController(ISessionService sessionService, IUnitOfWork unitOfWork)
        {
            _sessionService = sessionService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IActionResult> Scan(int tableNumber)
        {
            // Cache table number for 12 hours in a cookie
            CookieOptions option = new CookieOptions
            {
                Expires = DateTime.Now.AddHours(12),
                HttpOnly = true,
                Secure = true // Set to true if using HTTPS
            };
            Response.Cookies.Append("TableNumber", tableNumber.ToString(), option);

            var tables = await _unitOfWork.Repository<RestaurantTable>().ListAsync(t => t.TableNumber == tableNumber);
            var table = tables.FirstOrDefault();
            
            if (table == null) return NotFound("Table not found");

            // Check if guest already has valid cookies from a previous session today
            string guestName = Request.Cookies["GuestName"];
            string guestPhone = Request.Cookies["GuestPhone"];

            var activeSession = await _sessionService.GetActiveSessionByTableAsync(table.Id);
            
            if (activeSession != null)
            {
                // Table is occupied. If cookies match, let them directly in!
                if (!string.IsNullOrEmpty(guestName) && !string.IsNullOrEmpty(guestPhone) &&
                    string.Equals(activeSession.CustomerName, guestName, StringComparison.OrdinalIgnoreCase) && 
                    activeSession.PhoneNumber == guestPhone)
                {
                    return RedirectToAction("Index", "Menu");
                }
            }
            else
            {
                // Table is free. If they have cookies, auto-start a new session!
                if (!string.IsNullOrEmpty(guestName) && !string.IsNullOrEmpty(guestPhone))
                {
                    await _sessionService.StartSessionAsync(table.Id, guestName, guestPhone);
                    return RedirectToAction("Index", "Menu");
                }
            }

            // No valid cookies or mismatch, redirect to Register to verify identity or start new session
            return RedirectToAction("Register");
        }

        [HttpGet]
        public IActionResult Register()
        {
            string tableNumberStr = Request.Cookies["TableNumber"];
            if (string.IsNullOrEmpty(tableNumberStr))
            {
                return RedirectToAction("Index", "Home");
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
