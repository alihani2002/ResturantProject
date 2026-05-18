using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using Resturant.Web.UI.Hubs;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Accountant + "," + AppRoles.Admin + "," + AppRoles.Manager)]
    public class CashierController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;

        public CashierController(AppDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index()
        {
            var activeOrders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Ready || o.Status == OrderStatus.InPreparation || o.Status == OrderStatus.Served)
                .OrderBy(o => o.OrderDate)
                .ToListAsync();

            // We will pass the raw list but the View will handle grouping or we can group here.
            // For consistency with GetOrdersData, let's prepare the grouped data.
            return View(activeOrders);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrdersData()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Ready || o.Status == OrderStatus.InPreparation || o.Status == OrderStatus.Served)
                .ToListAsync();

            // Group by TableSessionId (if present) or TableNumber (fallback)
            var groupedOrders = orders.GroupBy(o => o.TableSessionId ?? -o.TableNumber)
                .Select(g => new
                {
                    sessionId = g.Key > 0 ? g.Key : (int?)null,
                    tableNumber = g.First().TableNumber,
                    customerName = g.First().CustomerName ?? "Guest",
                    phoneNumber = g.First().PhoneNumber ?? "N/A",
                    status = g.Any(o => o.Status == OrderStatus.Ready) ? (int)OrderStatus.Ready : (int)g.First().Status,
                    totalAmount = g.Sum(o => o.TotalAmount),
                    orderIds = g.Select(o => o.Id).ToList(),
                    orderItems = g.SelectMany(o => o.OrderItems).Select(oi => new
                    {
                        id = oi.Id,
                        menuItemName = oi.MenuItem != null ? oi.MenuItem.Name : "",
                        quantity = oi.Quantity,
                        price = oi.Price
                    })
                })
                .ToList();

            return Json(groupedOrders);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment(string orderIds)
        {
            if (string.IsNullOrEmpty(orderIds)) return BadRequest();
            
            var ids = orderIds.Split(',').Select(int.Parse).ToList();
            var orders = await _context.Orders.Where(o => ids.Contains(o.Id)).ToListAsync();
            
            if (orders.Any())
            {
                int? sessionId = orders.First().TableSessionId;
                int tableNumber = orders.First().TableNumber;

                foreach (var order in orders)
                {
                    order.Status = OrderStatus.Completed;
                    order.CompletedDate = DateTime.Now;
                }

                // If all orders for this session are paid, optionally close the session
                // But usually, payment IS the trigger to clear the table.
                if (sessionId.HasValue)
                {
                    var session = await _context.Set<TableSession>().FindAsync(sessionId.Value);
                    if (session != null)
                    {
                        session.IsActive = false;
                        session.EndTime = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                var notifyData = new { tableNumber = tableNumber };

                // Notify all dashboards and guest tracking
                await _hubContext.Clients.Group("waiter").SendAsync("TableCleared", notifyData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderCompleted", notifyData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", notifyData);
                await _hubContext.Clients.All.SendAsync("OrderStatusChanged", new { tableNumber = tableNumber });

                return Ok();
            }
            return NotFound();
        }

        public async Task<IActionResult> PaidOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == OrderStatus.Completed)
                .OrderByDescending(o => o.CompletedDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Receipt(string ids)
        {
            if (string.IsNullOrEmpty(ids)) return NotFound();
            
            var orderIds = ids.Split(',').Select(int.Parse).ToList();
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.AddOns)
                        .ThenInclude(a => a.AddOn)
                .Where(o => orderIds.Contains(o.Id))
                .ToListAsync();

            if (!orders.Any()) return NotFound();

            // Pass the first order to get Customer info (they should all be same for same table)
            ViewBag.AllOrders = orders;
            return View(orders.First());
        }
    }
}