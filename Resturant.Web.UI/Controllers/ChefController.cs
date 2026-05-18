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
    [Authorize(Roles = AppRoles.Chief + "," + AppRoles.Admin + "," + AppRoles.Manager)]
    public class ChefController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;

        public ChefController(AppDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.AddOns)
                        .ThenInclude(a => a.AddOn)
                .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.InPreparation)
                .OrderBy(o => o.ConfirmedDate)
                .ToListAsync();

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrdersData()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.AddOns)
                        .ThenInclude(a => a.AddOn)
                .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.InPreparation)
                .OrderBy(o => o.ConfirmedDate)
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
                        price = oi.Price,
                        addOns = oi.AddOns.Select(a => new
                        {
                            id = a.Id,
                            name = a.AddOn != null ? a.AddOn.Name : ""
                        }).ToList()
                    })
                })
                .ToListAsync();

            return Json(orders);
        }

        [HttpPost]
        public async Task<IActionResult> FinishOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null)
            {
                order.Status = OrderStatus.Ready;
                await _context.SaveChangesAsync();

                var orderData = new
                {
                    id = order.Id,
                    tableNumber = order.TableNumber,
                    status = (int)order.Status,
                    totalAmount = order.TotalAmount,
                    orderDate = order.OrderDate,
                    orderItems = order.OrderItems.Select(oi => new
                    {
                        id = oi.Id,
                        menuItemName = oi.MenuItem?.Name,
                        quantity = oi.Quantity,
                        price = oi.Price
                    })
                };

                // Notify all dashboards and guest tracking
                await _hubContext.Clients.Group("waiter").SendAsync("OrderReady", orderData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderReady", orderData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
                await _hubContext.Clients.All.SendAsync("OrderStatusChanged", orderData);

                // Legacy events
                await _hubContext.Clients.All.SendAsync("ReceiveAccountantUpdate", "New Order Ready for Payment");
                await _hubContext.Clients.All.SendAsync("ReceiveWaiterUpdate", "Order Ready");
            }
            return Ok();
        }
    }
}