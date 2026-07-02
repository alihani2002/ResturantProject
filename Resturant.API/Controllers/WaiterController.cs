using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Resturant.API.Hubs;
using Resturant.API.Models;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Resturant.API.Controllers
{
    [Authorize(Roles = AppRoles.Waiter + "," + AppRoles.Admin + "," + AppRoles.Manager)]
    [ApiController]
    [Route("api/[controller]")]
    public class WaiterController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<OrderHub> _hubContext;

        public WaiterController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        private async Task<int> GetBranchIdAsync(int? queryBranchId = null)
        {
            if (queryBranchId.HasValue && queryBranchId.Value > 0 && 
                (User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager)))
            {
                return queryBranchId.Value;
            }

            var userBranchClaim = User.FindFirst("BranchId")?.Value;
            if (int.TryParse(userBranchClaim, out int branchId) && branchId > 0)
            {
                return branchId;
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null && user.BranchId.HasValue)
                {
                    return user.BranchId.Value;
                }
            }

            return 1; // Default fallback
        }

        // GET: api/waiter/tables
        [HttpGet("tables")]
        public async Task<IActionResult> GetTables([FromQuery] int? branchId)
        {
            int selectedBranchId = await GetBranchIdAsync(branchId);
            var isWaiter = User.IsInRole(AppRoles.Waiter);
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var query = _context.RestaurantTables.Where(t => t.BranchId == selectedBranchId);
            if (isWaiter && !string.IsNullOrEmpty(userId))
            {
                query = query.Where(t => t.WaiterId == userId);
            }

            var tables = await query.OrderBy(t => t.TableNumber).ToListAsync();
            
            var activeSessions = await _context.Set<TableSession>()
                .Where(s => s.IsActive && s.BranchId == selectedBranchId)
                .ToListAsync();

            var activeOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled && o.BranchId == selectedBranchId)
                .ToListAsync();

            var result = tables.Select(table =>
            {
                var session = activeSessions.FirstOrDefault(s => s.TableId == table.Id);
                var tableOrders = activeOrders.Where(o => o.TableNumber == table.TableNumber).ToList();
                
                string status = "Empty";
                if (tableOrders.Any())
                {
                    status = tableOrders.Any(o => o.Status == OrderStatus.Pending) ? "Pending" : "Active";
                }

                return new TableResponse
                {
                    Id = table.Id,
                    TableNumber = table.TableNumber,
                    Status = status,
                    SessionId = session?.Id,
                    CustomerName = session?.CustomerName,
                    PhoneNumber = session?.PhoneNumber
                };
            }).ToList();

            return Ok(result);
        }

        // POST: api/waiter/sessions/start
        [HttpPost("sessions/start")]
        public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request)
        {
            if (request == null || request.TableNumber <= 0)
            {
                return BadRequest("Invalid table number");
            }

            int branchId = await GetBranchIdAsync(null);

            var table = await _context.RestaurantTables
                .FirstOrDefaultAsync(t => t.TableNumber == request.TableNumber && t.BranchId == branchId);
            if (table == null) return NotFound($"Table {request.TableNumber} not found in this branch");

            var activeSession = await _context.Set<TableSession>()
                .FirstOrDefaultAsync(s => s.TableId == table.Id && s.IsActive);
            if (activeSession != null)
            {
                return Ok(new { sessionId = activeSession.Id, message = "Table already has an active session" });
            }

            var newSession = new TableSession
            {
                TableId = table.Id,
                CustomerName = string.IsNullOrEmpty(request.CustomerName) ? $"Table {request.TableNumber} Guest" : request.CustomerName,
                PhoneNumber = string.IsNullOrEmpty(request.PhoneNumber) ? "0000000000" : request.PhoneNumber,
                StartTime = DateTime.Now,
                IsActive = true,
                BranchId = branchId
            };

            _context.Set<TableSession>().Add(newSession);
            await _context.SaveChangesAsync();

            // Real-time notification
            var notifyPayload = new { branchId = branchId, tableNumber = table.TableNumber, customerName = newSession.CustomerName, eventType = "GuestSeated" };
            await _hubContext.Clients.Group($"waiter_{branchId}").SendAsync("ReceiveWaiterUpdate", notifyPayload);
            await _hubContext.Clients.Group($"admin_{branchId}").SendAsync("ReceiveWaiterUpdate", notifyPayload);

            return Ok(new { sessionId = newSession.Id, message = "Session started successfully." });
        }

        // POST: api/waiter/sessions/{sessionId}/close
        [HttpPost("sessions/{sessionId}/close")]
        public async Task<IActionResult> CloseSession(int sessionId)
        {
            var session = await _context.Set<TableSession>()
                .Include(s => s.Table)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null || !session.IsActive)
            {
                return NotFound("Active session not found.");
            }

            session.IsActive = false;
            session.EndTime = DateTime.Now;

            // Complete all active orders for this session
            var orders = await _context.Orders
                .Where(o => o.TableSessionId == sessionId && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            foreach (var order in orders)
            {
                order.Status = OrderStatus.Completed;
                order.CompletedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            var notifyData = new { tableNumber = session.Table?.TableNumber ?? 0 };
            await _hubContext.Clients.Group($"waiter_{session.BranchId}").SendAsync("TableCleared", notifyData);
            await _hubContext.Clients.Group($"cashier_{session.BranchId}").SendAsync("OrderCompleted", notifyData);
            await _hubContext.Clients.Group($"admin_{session.BranchId}").SendAsync("OrderStatusChanged", notifyData);

            return Ok(new { message = "Session closed successfully and active orders marked completed." });
        }

        // GET: api/waiter/menu
        [HttpGet("menu")]
        public async Task<IActionResult> GetMenu([FromQuery] int? branchId)
        {
            int selectedBranchId = await GetBranchIdAsync(branchId);

            var categories = await _context.MenuCategories
                .Include(c => c.MenuItems)
                    .ThenInclude(m => m.Sizes)
                .Include(c => c.MenuItems)
                    .ThenInclude(m => m.AddOns)
                .Where(c => c.IsActive && c.BranchId == selectedBranchId && c.MenuItems.Any(mi => mi.IsAvailable))
                .OrderBy(c => c.OrderNumber)
                .ToListAsync();

            var menu = categories.Select(c => new
            {
                categoryId = c.Id,
                categoryName = c.Name,
                items = c.MenuItems.Where(mi => mi.IsAvailable && mi.BranchId == selectedBranchId).Select(mi => new
                {
                    id = mi.Id,
                    name = mi.Name,
                    price = mi.Price,
                    description = mi.Description,
                    sizes = mi.GetParsedSizes().Select(s => new { name = s.Name, price = s.Price }).ToList(),
                    addOns = mi.AddOns.Where(a => a.IsAvailable).Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        price = a.ExtraPrice
                    }).ToList()
                }).OrderBy(mi => mi.name).ToList()
            }).ToList();

            return Ok(menu);
        }

        // POST: api/waiter/orders
        [HttpPost("orders")]
        public async Task<IActionResult> CreateOrder([FromBody] ApiCreateOrderRequest request)
        {
            if (request == null || request.TableNumber <= 0 || request.OrderItems == null || request.OrderItems.Count == 0)
            {
                return BadRequest("Invalid order data");
            }

            int branchId = await GetBranchIdAsync(request.BranchId);

            var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.TableNumber == request.TableNumber && t.BranchId == branchId);
            if (table == null)
            {
                return BadRequest($"Table {request.TableNumber} does not exist in branch {branchId}");
            }

            var activeSession = await _context.Set<TableSession>()
                .FirstOrDefaultAsync(s => s.TableId == table.Id && s.IsActive);
            if (activeSession == null)
            {
                return BadRequest("No active session found for this table. Please seat the guest first.");
            }

            // Creating the order. Since a Waiter creates it, it starts as Confirmed
            var order = new Order
            {
                TableNumber = request.TableNumber,
                TableSessionId = activeSession.Id,
                Status = OrderStatus.Confirmed,
                OrderDate = DateTime.Now,
                ConfirmedDate = DateTime.Now,
                Note = request.Note,
                CustomerName = activeSession.CustomerName,
                PhoneNumber = activeSession.PhoneNumber,
                PriceCategory = string.IsNullOrWhiteSpace(request.PriceCategory) ? activeSession.PriceCategory : request.PriceCategory,
                BranchId = table.BranchId
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            decimal totalAmount = 0;
            var orderItemsEntities = new List<OrderItem>();

            foreach (var itemRequest in request.OrderItems)
            {
                var menuItem = await _context.MenuItems
                    .Include(m => m.Sizes)
                    .Include(m => m.Prices)
                    .FirstOrDefaultAsync(m => m.Id == itemRequest.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                {
                    continue;
                }

                var resolvedPrice = ResolveMenuItemPrice(menuItem, order.BranchId, order.PriceCategory, itemRequest.Quantity, activeSession.PhoneNumber);
                var itemPrice = itemRequest.Price.HasValue ? itemRequest.Price.Value : resolvedPrice;
                
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

                // Add AddOns
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

            var settings = await _context.RestaurantSettings.FirstOrDefaultAsync(s => s.BranchId == order.BranchId) 
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

            // Prepare SignalR Data
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

            // Broadcast real-time notifications
            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("NewOrderReceived", orderData);
            await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("NewOrderReceived", orderData);
            await _hubContext.Clients.Group($"chef_{order.BranchId}").SendAsync("OrderAccepted", orderData);
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderAccepted", orderData);

            return Ok(new OrderResponse
            {
                OrderId = order.Id,
                TableNumber = order.TableNumber ?? 0,
                SessionId = order.TableSessionId,
                CustomerName = order.CustomerName ?? "",
                PhoneNumber = order.PhoneNumber ?? "",
                TotalAmount = order.TotalAmount,
                Status = order.Status.ToString(),
                OrderDate = order.OrderDate,
                Note = order.Note,
                Items = orderItemsEntities.Select(oi => new OrderItemResponse
                {
                    Id = oi.Id,
                    Name = _context.MenuItems.Find(oi.MenuItemId)?.Name ?? "Item",
                    Quantity = oi.Quantity,
                    Price = oi.Price,
                    IsCancelled = oi.IsCancelled,
                    AddOns = string.Join(", ", oi.AddOns.Select(a => _context.MenuItemAddOns.Find(a.MenuItemAddOnId)?.Name ?? ""))
                }).ToList()
            });
        }

        // POST: api/waiter/orders/{orderId}/status
        [HttpPost("orders/{orderId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, [FromQuery] OrderStatus status)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound("Order not found");

            if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            {
                return BadRequest($"Order #{orderId} is already {order.Status} and cannot be modified.");
            }

            order.Status = status;

            var orderData = new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                status = status.ToString(),
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                note = order.Note
            };

            switch (status)
            {
                case OrderStatus.Confirmed:
                    order.ConfirmedDate = DateTime.Now;
                    await _hubContext.Clients.Group($"chef_{order.BranchId}").SendAsync("NewOrderReceived", orderData);
                    await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("NewOrderReceived", orderData);
                    break;
                case OrderStatus.InPreparation:
                    break;
                case OrderStatus.Ready:
                    await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
                    await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
                    break;
                case OrderStatus.Completed:
                    order.CompletedDate = DateTime.Now;
                    break;
                case OrderStatus.Served:
                    await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
                    break;
                case OrderStatus.Cancelled:
                    order.CancelledDate = DateTime.Now;
                    break;
            }

            await _hubContext.Clients.Group($"guest_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Order status updated to {status}", status = order.Status.ToString() });
        }

        // POST: api/waiter/orders/{orderId}/serve
        [HttpPost("orders/{orderId}/serve")]
        public async Task<IActionResult> ServeOrder(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound("Order not found");

            if (order.Status != OrderStatus.Ready)
            {
                return BadRequest("Only orders in 'Ready' state can be served.");
            }

            order.Status = OrderStatus.Served;
            await _context.SaveChangesAsync();

            var orderData = new { id = order.Id, tableNumber = order.TableNumber, status = order.Status.ToString() };
            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"guest_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);

            return Ok(new { message = "Order served successfully." });
        }

        // POST: api/waiter/orders/{orderId}/cancel
        [HttpPost("orders/{orderId}/cancel")]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound("Order not found");

            if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            {
                return BadRequest("Completed or cancelled orders cannot be cancelled.");
            }

            order.Status = OrderStatus.Cancelled;
            order.CancelledDate = DateTime.Now;
            await _context.SaveChangesAsync();

            var orderData = new { id = order.Id, tableNumber = order.TableNumber, status = order.Status.ToString() };
            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("TableCleared", orderData);
            await _hubContext.Clients.Group($"chef_{order.BranchId}").SendAsync("OrderCancelled", orderData);
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderCompleted", orderData);
            await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"guest_{order.BranchId}").SendAsync("TableCleared", orderData);

            return Ok(new { message = "Order cancelled successfully." });
        }

        // POST: api/waiter/order-items/{orderItemId}/cancel
        [HttpPost("order-items/{orderItemId}/cancel")]
        public async Task<IActionResult> CancelOrderItem(int orderItemId)
        {
            var orderItem = await _context.OrderItems
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi2 => oi2.MenuItem)
                .FirstOrDefaultAsync(oi => oi.Id == orderItemId);

            if (orderItem == null) return NotFound("Order item not found");
            if (orderItem.IsCancelled) return BadRequest("Item is already cancelled");

            orderItem.IsCancelled = true;

            var order = orderItem.Order;
            if (order != null)
            {
                decimal subtotal = 0;
                foreach (var oi in order.OrderItems.Where(item => !item.IsDeleted && !item.IsCancelled))
                {
                    decimal addOnsTotal = oi.AddOns.Where(a => !a.IsDeleted).Sum(a => a.Price);
                    subtotal += (oi.Price + addOnsTotal) * oi.Quantity;
                }

                decimal serviceAmount = subtotal * (order.ServicePercentage / 100);
                decimal taxAmount = (subtotal + serviceAmount) * (order.TaxPercentage / 100);
                decimal grandTotal = subtotal + serviceAmount + taxAmount;

                order.ServiceAmount = serviceAmount;
                order.TaxAmount = taxAmount;
                order.TotalAmount = grandTotal;
            }

            await _context.SaveChangesAsync();

            var itemCancelledData = new
            {
                orderId = order?.Id,
                tableNumber = order?.TableNumber,
                cancelledItemId = orderItemId,
                cancelledItemName = orderItem.MenuItem?.Name ?? "Item"
            };

            if (order != null)
            {
                await _hubContext.Clients.Group($"chef_{order.BranchId}").SendAsync("OrderItemCancelled", itemCancelledData);
                await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderItemCancelled", itemCancelledData);
                await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("OrderStatusChanged", new { tableNumber = order.TableNumber });
            }

            return Ok(new { message = $"Item '{orderItem.MenuItem?.Name}' cancelled successfully." });
        }

        // --- Helper Methods for Pricing ---
        private static decimal ResolveMenuItemPrice(MenuItem menuItem, int branchId, string? priceCategory, int quantity, string? customerKey)
        {
            var now = DateTime.Now;
            var category = string.IsNullOrWhiteSpace(priceCategory) ? "Retail" : priceCategory;
            var price = menuItem.Prices
                .Where(p => p.IsActive && !p.IsDeleted)
                .Where(p => !p.StartsOn.HasValue || p.StartsOn.Value <= now)
                .Where(p => !p.EndsOn.HasValue || p.EndsOn.Value >= now)
                .Where(p => !p.BranchId.HasValue || p.BranchId.Value == branchId)
                .Where(p => !p.MinQuantity.HasValue || quantity >= p.MinQuantity.Value)
                .Where(p => string.IsNullOrWhiteSpace(p.CustomerKey) || p.CustomerKey == customerKey)
                .Where(p => p.PriceType == category || p.PriceType == "Retail")
                .OrderBy(p => p.PriceType == category ? 0 : 1)
                .ThenBy(p => p.BranchId.HasValue ? 0 : 1)
                .ThenBy(p => string.IsNullOrWhiteSpace(p.CustomerKey) ? 1 : 0)
                .ThenBy(p => p.Priority)
                .FirstOrDefault();

            return price?.Price ?? menuItem.Price;
        }
    }
}
