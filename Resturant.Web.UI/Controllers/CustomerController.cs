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
        public async Task<IActionResult> Order(int tableNumber)
        {
            var table = await _tableService.GetTableByNumberAsync(tableNumber);
            if (table == null) return NotFound("Table not found");

            var activeSession = await _sessionService.GetActiveSessionByTableAsync(table.Id);
            
            ViewBag.TableNumber = tableNumber;
            ViewBag.TableId = table.Id;

            if (activeSession == null)
            {
                return View("StartSession"); // View to enter Name/Phone
            }

            return RedirectToAction("Menu", new { sessionId = activeSession.Id });
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
