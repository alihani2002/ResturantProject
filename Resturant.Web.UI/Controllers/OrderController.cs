using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Resturant.Infrastructure.Data;
using Resturant.Core.Entities;
using Resturant.Web.UI.Hubs;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Resturant.Web.UI.Controllers
{
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;

        public OrderController(AppDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // POST: Order/Create
        [HttpPost]
        public async Task<IActionResult> Create(int tableNumber, [FromBody] List<OrderItem> orderItems)
        {
            if (tableNumber <= 0 || orderItems == null || orderItems.Count == 0)
            {
                return BadRequest("Invalid order data");
            }

            // Calculate total amount
            decimal totalAmount = 0;
            foreach (var item in orderItems)
            {
                var menuItem = await _context.MenuItems.FindAsync(item.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                {
                    return BadRequest($"MenuItem {item.MenuItemId} not found or not available");
                }
                item.Price = menuItem.Price;
                totalAmount += item.Total;
            }

            // Create order
            var order = new Order
            {
                TableNumber = tableNumber,
                Status = OrderStatus.Pending,
                TotalAmount = totalAmount,
                OrderDate = DateTime.Now
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Add order items
            foreach (var item in orderItems)
            {
                item.OrderId = order.Id;
                _context.OrderItems.Add(item);
            }

            await _context.SaveChangesAsync();

            // Send real-time update to waiter dashboard
            await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", "New order received");
            await _hubContext.Clients.All.SendAsync("ReceiveOrderDetails", order);

            return Ok(new { OrderId = order.Id, TotalAmount = totalAmount });
        }

        // GET: Order/GetOrdersByStatus
        [HttpGet]
        public async Task<IActionResult> GetOrdersByStatus(OrderStatus status)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == status)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return Json(orders);
        }

        // POST: Order/UpdateStatus
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, OrderStatus status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return NotFound();
            }

            order.Status = status;

            switch (status)
            {
                case OrderStatus.Confirmed:
                    order.ConfirmedDate = DateTime.Now;
                    await _hubContext.Clients.All.SendAsync("ReceiveChefUpdate", "Order ready for preparation");
                    break;
                case OrderStatus.InPreparation:
                    await _hubContext.Clients.All.SendAsync("ReceiveChefUpdate", "Order in preparation");
                    break;
                case OrderStatus.Ready:
                    await _hubContext.Clients.All.SendAsync("ReceiveWaiterUpdate", "Order ready to serve");
                    break;
                case OrderStatus.Completed:
                    order.CompletedDate = DateTime.Now;
                    await _hubContext.Clients.All.SendAsync("ReceiveCashierUpdate", "Order ready for checkout");
                    break;
                case OrderStatus.Cancelled:
                    order.CancelledDate = DateTime.Now;
                    break;
            }

            await _context.SaveChangesAsync();

            return Ok();
        }

        // GET: Order/GetOrderDetails
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return Json(order);
        }
    }
}