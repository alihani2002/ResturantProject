/* 
 * NOTE: didn't create migration or database
 */
using Microsoft.AspNetCore.Mvc;
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ITableService _tableService;
        private readonly ISessionService _sessionService;
        private readonly IOrderService _orderService;

        public CustomerController(
            ITableService tableService, 
            ISessionService sessionService,
            IOrderService orderService)
        {
            _tableService = tableService;
            _sessionService = sessionService;
            _orderService = orderService;
        }

        // URL: /Customer/Order/5
        public IActionResult Order(int tableNumber)
        {
            // Redirect legacy QR codes to the secure CustomerSession flow
            return RedirectToAction("Scan", "CustomerSession", new { tableNumber = tableNumber });
        }

        [HttpPost]
        public async Task<IActionResult> StartSession(int tableId, string name, string phone)
        {
            var session = await _sessionService.StartSessionAsync(tableId, name, phone);
            return RedirectToAction("Menu", new { sessionId = session.Id });
        }

        public IActionResult Menu(int sessionId)
        {
            // Load menu with recommendations and popular items
            return View();
        }

        public async Task<IActionResult> Track(string phone)
        {
            var orders = await _orderService.GetOrdersByPhoneAsync(phone);
            return View(orders);
        }
    }
}
