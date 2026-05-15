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
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            if (request == null || request.TableNumber <= 0 || request.OrderItems == null || request.OrderItems.Count == 0)
            {
                return BadRequest("Invalid order data");
            }

            // Check if table exists
            var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.TableNumber == request.TableNumber);
            if (table == null)
            {
                return BadRequest($"Table {request.TableNumber} does not exist");
            }

            // Get Active Session
            var activeSession = await _context.Set<TableSession>()
                .FirstOrDefaultAsync(s => s.TableId == table.Id && s.IsActive);
            
            if (activeSession == null)
            {
                return BadRequest("No active session found for this table. Please scan the QR code again.");
            }

            Order order = new Order
            {
                TableNumber = request.TableNumber,
                TableSessionId = activeSession.Id,
                Status = OrderStatus.Pending,
                OrderDate = DateTime.Now,
                Note = request.Note,
                CustomerName = activeSession.CustomerName,
                PhoneNumber = activeSession.PhoneNumber
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            decimal totalAmount = 0;
            var orderItemsEntities = new List<OrderItem>();

            foreach (var itemRequest in request.OrderItems)
            {
                var menuItem = await _context.MenuItems.FindAsync(itemRequest.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                {
                    continue; // Or handle error
                }
                
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    MenuItemId = itemRequest.MenuItemId,
                    Quantity = itemRequest.Quantity,
                    Price = menuItem.Price
                };

                _context.OrderItems.Add(orderItem);
                orderItemsEntities.Add(orderItem);
                totalAmount += orderItem.Total;
            }

            order.TotalAmount = totalAmount;
            await _context.SaveChangesAsync();

            // Prepare order data for SignalR
            var orderData = new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                status = order.Status.ToString(),
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                note = order.Note,
                items = orderItemsEntities.Select(oi => new
                {
                    menuItemName = _context.MenuItems.Find(oi.MenuItemId)?.Name ?? "Unknown",
                    quantity = oi.Quantity,
                    price = oi.Price,
                    total = oi.Total
                }).ToList()
            };

            await _hubContext.Clients.Group("waiter").SendAsync("NewOrderReceived", orderData);
            await _hubContext.Clients.Group("admin").SendAsync("NewOrderReceived", orderData);

            return Ok(new { OrderId = order.Id, TotalAmount = totalAmount });
        }

        // GET: Order/CheckTable - Check if table exists and is available
        [HttpGet]
        public async Task<IActionResult> CheckTable(int tableNumber)
        {
            var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.TableNumber == tableNumber);
            if (table == null)
            {
                return Json(new { available = false, message = $"Table {tableNumber} does not exist." });
            }

            // Check if there's an active session for this table
            var activeSession = await _context.Set<TableSession>()
                .FirstOrDefaultAsync(s => s.TableId == table.Id && s.IsActive);

            if (activeSession != null)
            {
                // If the current user has the cookie for this table, allow them in
                string cachedTable = Request.Cookies["TableNumber"];
                if (cachedTable == tableNumber.ToString())
                {
                    return Json(new { available = true, message = "Table session is active." });
                }
                
                return Json(new { available = false, message = $"Table {tableNumber} is occupied." });
            }

            return Json(new { available = true, message = "Table is free." });
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
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return NotFound();
            }

            // Prevent modifying orders that are already Completed or Cancelled
            if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            {
                // If the order is already final, we shouldn't allow moving it back to an active state
                // Exception: maybe if an admin wants to reopen it, but for now let's block it to fix the reported bug
                return BadRequest($"Order #{orderId} is already {order.Status} and cannot be modified.");
            }

            order.Status = status;

            // Prepare order data for SignalR
            var orderData = new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                status = status.ToString(),
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                note = order.Note,
                items = order.OrderItems.Select(oi => new
                {
                    menuItemName = oi.MenuItem?.Name ?? "Unknown",
                    quantity = oi.Quantity,
                    price = oi.Price,
                    total = oi.Total
                }).ToList()
            };

            switch (status)
            {
                case OrderStatus.Confirmed:
                    order.ConfirmedDate = DateTime.Now;
                    // Waiter confirmed -> notify Chef and Cashier
                    await _hubContext.Clients.Group("chef").SendAsync("NewOrderReceived", orderData);
                    await _hubContext.Clients.Group("cashier").SendAsync("NewOrderReceived", orderData);
                    await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.All.SendAsync("ReceiveChefUpdate", "Order ready for preparation");
                    break;
                case OrderStatus.InPreparation:
                    await _hubContext.Clients.Group("waiter").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.All.SendAsync("ReceiveChefUpdate", "Order in preparation");
                    break;
                case OrderStatus.Ready:
                    // Chef finished -> notify Waiter
                    await _hubContext.Clients.Group("waiter").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.Group("cashier").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.All.SendAsync("ReceiveWaiterUpdate", "Order ready to serve");
                    break;
                case OrderStatus.Completed:
                    order.CompletedDate = DateTime.Now;
                    // Cashier processed payment -> order is complete
                    await _hubContext.Clients.Group("waiter").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.All.SendAsync("ReceiveAccountantUpdate", "Order payment completed");
                    break;
                case OrderStatus.Served:
                    // Waiter served the order -> notify Cashier for payment
                    await _hubContext.Clients.Group("cashier").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
                    break;
                case OrderStatus.Cancelled:
                    order.CancelledDate = DateTime.Now;
                    await _hubContext.Clients.Group("waiter").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.Group("chef").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.Group("cashier").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
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

    public class CreateOrderRequest
    {
        public int TableNumber { get; set; }
        public string? Note { get; set; }
        public List<OrderItemRequest> OrderItems { get; set; }
    }

    public class OrderItemRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
    }
}