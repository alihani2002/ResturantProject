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
                    note = o.Note,
                    // Effective total (cancelled items are free)
                    totalAmount = o.OrderItems.Where(oi => !oi.IsCancelled).Sum(oi => oi.EffectiveTotal),
                    orderDate = o.OrderDate,
                    orderItems = o.OrderItems.Select(oi => new
                    {
                        id = oi.Id,
                        menuItemName = oi.MenuItem != null ? oi.MenuItem.GetFormattedNameWithPrice(oi.Price) : "",
                        quantity = oi.Quantity,
                        price = oi.Price,
                        isCancelled = oi.IsCancelled,
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

        // Chef clicks "Start Preparing" → sets InPreparation
        [HttpPost]
        public async Task<IActionResult> StartOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();
            if (order.Status != OrderStatus.Confirmed) return BadRequest();

            order.Status = OrderStatus.InPreparation;
            await _context.SaveChangesAsync();

            var orderData = BuildOrderData(order);

            await _hubContext.Clients.Group("waiter").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group("cashier").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.All.SendAsync("OrderStatusChanged", orderData);

            return Ok();
        }

        // Chef clicks "Mark Ready" → sets Ready, removes from chef board
        [HttpPost]
        public async Task<IActionResult> FinishOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();
            if (order.Status == OrderStatus.Ready || order.Status == OrderStatus.Served || order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            {
                return BadRequest("Order is already ready or completed.");
            }

            order.Status = OrderStatus.Ready;
            await _context.SaveChangesAsync();

            var orderData = BuildOrderData(order);

            await _hubContext.Clients.Group("waiter").SendAsync("OrderReady", orderData);
            await _hubContext.Clients.Group("cashier").SendAsync("OrderReady", orderData);
            await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.All.SendAsync("OrderStatusChanged", orderData);

            await _hubContext.Clients.All.SendAsync("ReceiveAccountantUpdate", "New Order Ready for Payment");
            await _hubContext.Clients.All.SendAsync("ReceiveWaiterUpdate", "Order Ready");

            return Ok();
        }

        private object BuildOrderData(Order order)
        {
            return new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                status = (int)order.Status,
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                note = order.Note,
                orderItems = order.OrderItems.Select(oi => new
                {
                    id = oi.Id,
                    menuItemName = oi.MenuItem?.GetFormattedNameWithPrice(oi.Price) ?? "",
                    quantity = oi.Quantity,
                    price = oi.Price,
                    isCancelled = oi.IsCancelled
                })
            };
        }
    }
}