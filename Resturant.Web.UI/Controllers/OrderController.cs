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

            // Strategy: 
            // 1. Look for an existing "Initiated" session (Lock) to promote to a real order.
            // 2. If no Lock, check if table is valid for Multi-Order (i.e., user already Occupies it).
            // Note: We don't block invalid access here because strict blocking happens at CheckTable entry.

            var initiatedOrder = await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.TableNumber == request.TableNumber 
                    && o.Status == OrderStatus.Initiated)
                .OrderByDescending(o => o.OrderDate)
                .FirstOrDefaultAsync();

            Order order;
            decimal totalAmount = 0;
            var orderItemsEntities = new List<OrderItem>();

            // Calculate totals first
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

            if (initiatedOrder != null)
            {
                // Promote the Lock to a Real Order
                order = initiatedOrder;
                order.Status = OrderStatus.Pending;
                order.OrderDate = DateTime.Now; // Update timestamp to actual order time
                order.TotalAmount = totalAmount;
                order.Note = request.Note;
                
                // Clear any existing dummy items if any (though lock shouldn't have any)
                _context.OrderItems.RemoveRange(order.OrderItems);
            }
            else
            {
                // Check if table is occupied by REAL orders (Multi-Support) or Empty
                // If it's occupied by someone else, we technically allow this as "Multi-Order" 
                // assuming the frontend timeout prevented stale users.
                
                order = new Order
                {
                    TableNumber = request.TableNumber,
                    Status = OrderStatus.Pending,
                    TotalAmount = totalAmount,
                    OrderDate = DateTime.Now,
                    Note = request.Note
                };
                _context.Orders.Add(order);
            }

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
                note = order.Note,
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

            // Clean up expired locks (older than 5 minutes)
            var expiredLocks = await _context.Orders
                .Where(o => o.TableNumber == tableNumber && o.Status == OrderStatus.Initiated && o.OrderDate < DateTime.Now.AddMinutes(-5))
                .ToListAsync();
            
            if (expiredLocks.Any())
            {
                foreach (var lockOrder in expiredLocks)
                {
                    lockOrder.Status = OrderStatus.Cancelled;
                    lockOrder.CancelledDate = DateTime.Now;
                    lockOrder.Note = "Session Expired";
                }
                await _context.SaveChangesAsync();
            }

            // Check for strict occupancy
            var activeOrder = await _context.Orders
                .Where(o => o.TableNumber == tableNumber
                    && o.Status != OrderStatus.Completed
                    && o.Status != OrderStatus.Cancelled)
                .OrderByDescending(o => o.OrderDate)
                .FirstOrDefaultAsync();

            if (activeOrder != null)
            {
                // If the active order is Initiated (and not expired since we just cleaned them), access is denied
                if (activeOrder.Status == OrderStatus.Initiated)
                {
                    return Json(new { available = false, message = $"Table {tableNumber} is currently being viewed by another guest." });
                }
                
                // If active order is Pending/Active, access is denied (Strict Locking)
                return Json(new { available = false, message = $"Table {tableNumber} is occupied. Please wait until orders are paid." });
            }

            // Table is free, Create a Lock (Initiated Order)
            var lockSession = new Order
            {
                TableNumber = tableNumber,
                Status = OrderStatus.Initiated,
                OrderDate = DateTime.Now,
                TotalAmount = 0,
                Note = "Browsing Menu"
            };
            _context.Orders.Add(lockSession);
            await _context.SaveChangesAsync();

            return Json(new { available = true, message = "Table is available.", lockId = lockSession.Id });
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