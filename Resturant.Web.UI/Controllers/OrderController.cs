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

            // Determine active branch context (Staff user's assigned branch OR guest's cookie branch)
            int branchId = 1;
            if (User?.Identity != null && User.Identity.IsAuthenticated)
            {
                var userName = User.Identity.Name;
                var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (dbUser != null && dbUser.BranchId.HasValue)
                {
                    branchId = dbUser.BranchId.Value;
                }
            }
            else
            {
                string branchIdStr = Request.Cookies["BranchId"];
                if (!string.IsNullOrEmpty(branchIdStr) && int.TryParse(branchIdStr, out int cachedBranchId))
                {
                    branchId = cachedBranchId;
                }
            }

            // Check if table exists in this branch
            var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.TableNumber == request.TableNumber && t.BranchId == branchId);
            if (table == null)
            {
                return BadRequest($"Table {request.TableNumber} does not exist in this branch");
            }

            // Get Active Session
            var activeSession = await _context.Set<TableSession>()
                .FirstOrDefaultAsync(s => s.TableId == table.Id && s.IsActive);
            
            if (activeSession == null)
            {
                return BadRequest("No active session found for this table. Please scan the QR code again.");
            }

            // Prevent duplicate customer orders in rapid succession (within 5 seconds)
            var recentOrder = await _context.Orders
                .Where(o => o.TableSessionId == activeSession.Id && o.OrderDate >= DateTime.Now.AddSeconds(-5))
                .OrderByDescending(o => o.OrderDate)
                .FirstOrDefaultAsync();

            if (recentOrder != null && recentOrder.Note == request.Note)
            {
                var recentItems = await _context.OrderItems
                    .Where(oi => oi.OrderId == recentOrder.Id)
                    .OrderBy(oi => oi.MenuItemId)
                    .ToListAsync();

                var newItems = request.OrderItems.OrderBy(oi => oi.MenuItemId).ToList();

                if (recentItems.Count == newItems.Count)
                {
                    bool isIdentical = true;
                    for (int i = 0; i < recentItems.Count; i++)
                    {
                        if (recentItems[i].MenuItemId != newItems[i].MenuItemId || 
                            recentItems[i].Quantity != newItems[i].Quantity)
                        {
                            isIdentical = false;
                            break;
                        }
                    }

                    if (isIdentical)
                    {
                        return Ok(new { OrderId = recentOrder.Id, TotalAmount = recentOrder.TotalAmount });
                    }
                }
            }

            bool isStaff = User?.Identity != null && User.Identity.IsAuthenticated && 
                           (User.IsInRole(Resturant.Core.Common.AppRoles.Waiter) || 
                            User.IsInRole(Resturant.Core.Common.AppRoles.Admin) || 
                            User.IsInRole(Resturant.Core.Common.AppRoles.Manager));

            Order order = new Order
            {
                TableNumber = request.TableNumber,
                TableSessionId = activeSession.Id,
                Status = isStaff ? OrderStatus.Confirmed : OrderStatus.Pending,
                OrderDate = DateTime.Now,
                ConfirmedDate = isStaff ? DateTime.Now : null,
                Note = request.Note,
                CustomerName = activeSession.CustomerName,
                PhoneNumber = activeSession.PhoneNumber,
                BranchId = branchId
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            decimal totalAmount = 0;
            var orderItemsEntities = new List<OrderItem>();

            foreach (var itemRequest in request.OrderItems)
            {
                var menuItem = await _context.MenuItems
                    .Include(m => m.Sizes)
                    .FirstOrDefaultAsync(m => m.Id == itemRequest.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                {
                    continue; // Or handle error
                }

                var itemPrice = itemRequest.Price ?? menuItem.Price;
                var selectedSize = menuItem.GetParsedSizes().FirstOrDefault(s =>
                    (!string.IsNullOrWhiteSpace(itemRequest.Size) && s.Name == itemRequest.Size) ||
                    (string.IsNullOrWhiteSpace(itemRequest.Size) && s.Price == itemPrice));
                
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    MenuItemId = itemRequest.MenuItemId,
                    Quantity = itemRequest.Quantity,
                    Price = itemPrice,
                    MenuItemSizeId = selectedSize?.Id,
                    SizeName = selectedSize?.Name ?? itemRequest.Size,
                    UnitCost = selectedSize?.Cost ?? menuItem.Cost
                };

                _context.OrderItems.Add(orderItem);

                // Add AddOns if selected
                decimal addOnsTotal = 0;
                if (itemRequest.AddOnIds != null && itemRequest.AddOnIds.Any())
                {
                    var dbAddOns = await _context.MenuItemAddOns
                        .Where(a => itemRequest.AddOnIds.Contains(a.Id) && a.MenuItemId == itemRequest.MenuItemId)
                        .ToListAsync();

                    foreach (var addon in dbAddOns)
                    {
                        var orderItemAddOn = new OrderItemAddOn
                        {
                            OrderItem = orderItem,
                            MenuItemAddOnId = addon.Id,
                            Price = addon.ExtraPrice
                        };
                        _context.OrderItemAddOns.Add(orderItemAddOn);
                        orderItem.AddOns.Add(orderItemAddOn);
                        addOnsTotal += addon.ExtraPrice;
                    }
                }

                orderItemsEntities.Add(orderItem);
                totalAmount += (orderItem.Price + addOnsTotal) * orderItem.Quantity;
            }

            // Load branch-specific restaurant settings
            var settings = await _context.RestaurantSettings.FirstOrDefaultAsync(s => s.BranchId == branchId) 
                           ?? new RestaurantSetting { TaxPercentage = 14, ServicePercentage = 12 };

            decimal serviceAmount = totalAmount * (settings.ServicePercentage / 100);
            decimal taxAmount = (totalAmount + serviceAmount) * (settings.TaxPercentage / 100);
            decimal grandTotal = totalAmount + serviceAmount + taxAmount;

            order.TaxPercentage = settings.TaxPercentage;
            order.ServicePercentage = settings.ServicePercentage;
            order.ServiceAmount = serviceAmount;
            order.TaxAmount = taxAmount;
            order.TotalAmount = grandTotal;

            await _context.SaveChangesAsync();

            // Prepare order data for SignalR
            var orderData = new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                branchId = order.BranchId,
                status = order.Status.ToString(),
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                note = order.Note,
                items = orderItemsEntities.Select(oi => new
                {
                    menuItemName = _context.MenuItems.Find(oi.MenuItemId)?.GetFormattedNameWithSize(oi.SizeName, oi.Price) ?? "Unknown",
                    quantity = oi.Quantity,
                    price = oi.Price + oi.AddOns.Sum(a => a.Price),
                    total = oi.Total,
                    addOns = oi.AddOns.Select(a => new {
                        name = _context.MenuItemAddOns.Find(a.MenuItemAddOnId)?.Name ?? ""
                    }).ToList()
                }).ToList()
            };

            await _hubContext.Clients.Group("waiter").SendAsync("NewOrderReceived", orderData);
            await _hubContext.Clients.Group("admin").SendAsync("NewOrderReceived", orderData);

            if (order.Status == OrderStatus.Confirmed)
            {
                await _hubContext.Clients.Group("chef").SendAsync("OrderAccepted", orderData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderAccepted", orderData);
                await _hubContext.Clients.All.SendAsync("ReceiveChefUpdate", "New Order to Prepare");
            }

            return Ok(new { OrderId = order.Id, TotalAmount = totalAmount });
        }

        // GET: Order/CheckTable - Check if table exists and is available
        [HttpGet]
        public async Task<IActionResult> CheckTable(int tableNumber)
        {
            // Resolve branch from cookies if possible
            int branchId = 1;
            string branchIdStr = Request.Cookies["BranchId"];
            if (!string.IsNullOrEmpty(branchIdStr))
            {
                int.TryParse(branchIdStr, out branchId);
            }

            var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.TableNumber == tableNumber && t.BranchId == branchId);
            if (table == null)
            {
                return Json(new { available = false, message = $"Table {tableNumber} does not exist in this branch." });
            }

            return Json(new { available = true, message = "Table found, proceeding to secure verification." });
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
                    menuItemName = oi.MenuItem?.GetFormattedNameWithSize(oi.SizeName, oi.Price) ?? "Unknown",
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

            // Real-time broadcast to all clients (including guest tracking view)
            await _hubContext.Clients.All.SendAsync("OrderStatusChanged", orderData);

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

        // GET: Order/GetActiveSessionOrders
        [HttpGet]
        public async Task<IActionResult> GetActiveSessionOrders(int tableNumber)
        {
            // Resolve branch from cookies if possible
            int branchId = 1;
            string branchIdStr = Request.Cookies["BranchId"];
            if (!string.IsNullOrEmpty(branchIdStr))
            {
                int.TryParse(branchIdStr, out branchId);
            }

            var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.TableNumber == tableNumber && t.BranchId == branchId);
            if (table == null)
            {
                return BadRequest("Table not found");
            }

            var activeSession = await _context.Set<TableSession>()
                .FirstOrDefaultAsync(s => s.TableId == table.Id && s.IsActive);
            
            if (activeSession == null)
            {
                return Json(new List<object>());
            }

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.AddOns)
                        .ThenInclude(a => a.AddOn)
                .Where(o => o.TableSessionId == activeSession.Id)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    id = o.Id,
                    status = o.Status.ToString(),
                    statusInt = (int)o.Status,
                    totalAmount = o.TotalAmount,
                    orderDate = o.OrderDate,
                    note = o.Note,
                    items = o.OrderItems.Select(oi => new
                    {
                        name = oi.MenuItem != null ? oi.MenuItem.Name : "Unknown",
                        quantity = oi.Quantity,
                        price = oi.Price + oi.AddOns.Sum(a => a.Price),
                        addOns = oi.AddOns.Select(a => new { name = a.AddOn != null ? a.AddOn.Name : "" }).ToList()
                    }).ToList()
                })
                .ToListAsync();

            return Json(orders);
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
        public List<int>? AddOnIds { get; set; }
        public decimal? Price { get; set; }
        public string? Size { get; set; }
    }
}
