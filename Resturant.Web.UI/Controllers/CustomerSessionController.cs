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

            // Check if there is already an active session for this table
            var tables = await _unitOfWork.Repository<RestaurantTable>().ListAsync(t => t.TableNumber == tableNumber);
            var table = tables.FirstOrDefault();
            
            if (table == null) return NotFound("Table not found");

            var activeSession = await _sessionService.GetActiveSessionByTableAsync(table.Id);
            
            if (activeSession != null)
            {
                // Session already active, redirect to Menu
                return RedirectToAction("Index", "Menu");
            }

            // No active session, redirect to Register
            return RedirectToAction("Register");
        }

        [HttpGet]
        public IActionResult Register()
        {
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
            
            if (table == null) return NotFound("Table not found");

            await _sessionService.StartSessionAsync(table.Id, customerName, phoneNumber);
            
            return RedirectToAction("Index", "Menu");
        }
    }
}
