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
            var tableExists = await _context.RestaurantTables.AnyAsync(t => t.TableNumber == request.TableNumber);
            if (!tableExists)
            {
                return BadRequest($"Table {request.TableNumber} does not exist");
            }

            // Check if table is occupied (has an active order)
            var activeOrder = await _context.Orders
                .AnyAsync(o => o.TableNumber == request.TableNumber
                    && o.Status != OrderStatus.Completed
                    && o.Status != OrderStatus.Cancelled);
            if (activeOrder)
            {
                return BadRequest($"Table {request.TableNumber} is currently occupied. Please choose another table.");
            }

            // Calculate total amount and prepare order items
            decimal totalAmount = 0;
            var orderItemsEntities = new List<OrderItem>();

            foreach (var itemRequest in request.OrderItems)
            {
                var menuItem = await _context.MenuItems.FindAsync(itemRequest.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                {
                    return BadRequest($"MenuItem {itemRequest.MenuItemId} not found or not available");
                }
                
                var orderItem = new OrderItem
                {
                    MenuItemId = itemRequest.MenuItemId,
                    Quantity = itemRequest.Quantity,
                    Price = menuItem.Price
                };

                orderItemsEntities.Add(orderItem);
                totalAmount += orderItem.Total;
            }

            // Create order
            var order = new Order
            {
                TableNumber = request.TableNumber,
                Status = OrderStatus.Pending,
                TotalAmount = totalAmount,
                OrderDate = DateTime.Now
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Add order items
            foreach (var item in orderItemsEntities)
            {
                item.OrderId = order.Id;
                _context.OrderItems.Add(item);
            }

            await _context.SaveChangesAsync();

            // Populate OrderItems for SignalR response
            order.OrderItems = orderItemsEntities;

            // Prepare order data for SignalR
            var orderData = new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                status = order.Status.ToString(),
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                items = orderItemsEntities.Select(oi => new
                {
                    menuItemName = _context.MenuItems.Find(oi.MenuItemId)?.Name ?? "Unknown",
                    quantity = oi.Quantity,
                    price = oi.Price,
                    total = oi.Total
                }).ToList()
            };

            // Send real-time updates to all relevant dashboards using groups
            await _hubContext.Clients.Group("waiter").SendAsync("NewOrderReceived", orderData);
            await _hubContext.Clients.Group("admin").SendAsync("NewOrderReceived", orderData);

            // Legacy broadcast for backward compatibility
            await _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", "New order received");
            await _hubContext.Clients.All.SendAsync("ReceiveOrderDetails", orderData);

            return Ok(new { OrderId = order.Id, TotalAmount = totalAmount });
        }

        // GET: Order/CheckTable - Check if table exists and is available
        [HttpGet]
        public async Task<IActionResult> CheckTable(int tableNumber)
        {
            var tableExists = await _context.RestaurantTables.AnyAsync(t => t.TableNumber == tableNumber);
            if (!tableExists)
            {
                return Json(new { available = false, message = $"Table {tableNumber} does not exist." });
            }

            var isOccupied = await _context.Orders
                .AnyAsync(o => o.TableNumber == tableNumber
                    && o.Status != OrderStatus.Completed
                    && o.Status != OrderStatus.Cancelled);

            if (isOccupied)
            {
                return Json(new { available = false, message = $"Table {tableNumber} is currently occupied. Please choose another table." });
            }

            return Json(new { available = true, message = "Table is available." });
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

            order.Status = status;

            // Prepare order data for SignalR
            var orderData = new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                status = status.ToString(),
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
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
        public List<OrderItemRequest> OrderItems { get; set; }
    }

    public class OrderItemRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
    }
}