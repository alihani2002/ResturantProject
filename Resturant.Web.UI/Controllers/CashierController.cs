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
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Ready || o.Status == OrderStatus.InPreparation || o.Status == OrderStatus.Served)
                .OrderBy(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrdersData()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Ready || o.Status == OrderStatus.InPreparation || o.Status == OrderStatus.Served)
                .OrderBy(o => o.OrderDate)
                .Select(o => new
                {
                    id = o.Id,
                    tableNumber = o.TableNumber,
                    status = (int)o.Status,
                    totalAmount = o.TotalAmount,
                    orderDate = o.OrderDate,
                    orderItems = o.OrderItems.Select(oi => new
                    {
                        id = oi.Id,
                        menuItemName = oi.MenuItem != null ? oi.MenuItem.Name : "",
                        quantity = oi.Quantity,
                        price = oi.Price
                    })
                })
                .ToListAsync();

            return Json(orders);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.Status = OrderStatus.Completed;
                order.CompletedDate = DateTime.Now;
                await _context.SaveChangesAsync();

                var orderData = new
                {
                    id = order.Id,
                    tableNumber = order.TableNumber,
                    status = (int)order.Status,
                    totalAmount = order.TotalAmount
                };

                // Notify all dashboards
                await _hubContext.Clients.Group("waiter").SendAsync("PaymentProcessed", orderData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderCompleted", orderData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);

                // Legacy events
                await _hubContext.Clients.All.SendAsync("ReceiveWaiterUpdate", "Order Paid");
            }
            return Ok();
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

        public async Task<IActionResult> Receipt(int id)
        {
             var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }
    }
}