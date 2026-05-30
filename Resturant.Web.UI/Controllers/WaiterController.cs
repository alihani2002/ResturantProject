/* 
 * NOTE: didn't create migration or database
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using Resturant.Web.UI.Hubs;
using Resturant.Web.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Waiter + "," + AppRoles.Admin + "," + AppRoles.Manager)]
    public class WaiterController : Controller
    {
        private readonly AppDbContext _context;
/* 
 * NOTE: didn't create migration or database
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using Resturant.Web.UI.Hubs;
using Resturant.Web.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Waiter + "," + AppRoles.Admin + "," + AppRoles.Manager)]
    public class WaiterController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public WaiterController(AppDbContext context, IHubContext<OrderHub> hubContext, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _hubContext = hubContext;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var isWaiter = User.IsInRole(AppRoles.Waiter);

            var query = _context.RestaurantTables.AsQueryable();
            if (isWaiter)
            {
                query = query.Where(t => t.WaiterId == userId);
            }

            var tables = await query.OrderBy(t => t.TableNumber).ToListAsync();
            var activeSessions = await _context.Set<TableSession>()
                .Where(s => s.IsActive)
                .ToListAsync();
            var activeOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.AddOns)
                        .ThenInclude(a => a.AddOn)
                .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            var viewModels = new List<WaiterTableViewModel>();

            foreach (var table in tables)
            {
                var session = activeSessions.FirstOrDefault(s => s.TableId == table.Id);
                var tableOrders = activeOrders.Where(o => o.TableNumber == table.TableNumber).ToList();
                
                var viewModel = new WaiterTableViewModel
                {
                    TableId = table.Id,
                    TableNumber = table.TableNumber,
                    ActiveSessionId = session?.Id,
                    CustomerName = session?.CustomerName,
                    PhoneNumber = session?.PhoneNumber,
                    Orders = tableOrders.Select(o => new WaiterOrderViewModel 
                    {
                        OrderId = o.Id,
                        // Effective amount the customer will pay (excludes cancelled items)
                        TotalAmount = o.OrderItems.Where(oi => !oi.IsCancelled).Sum(oi => oi.EffectiveTotal),
                        OrderTime = o.OrderDate,
                        Note = o.Note,
                        Status = o.Status.ToString(),
                         OrderItems = o.OrderItems.Select(oi => new WaiterOrderItemViewModel
                         {
                             Name = oi.MenuItem?.Name ?? "Unknown",
                             Quantity = oi.Quantity,
                             Price = oi.Price,
                             IsCancelled = oi.IsCancelled,
                             AddOns = string.Join(", ", oi.AddOns.Select(a => a.AddOn?.Name ?? ""))
                         }).ToList()
                    }).ToList()
                };

                viewModels.Add(viewModel);
            }

            var sortedViewModels = viewModels
                .OrderByDescending(v => v.ActiveSessionId.HasValue)
                .ThenBy(v => v.TableNumber)
                .ToList();

            return View(sortedViewModels);
        }

        [HttpGet]
        public async Task<IActionResult> GetTablesData()
        {
            var userId = _userManager.GetUserId(User);
            var isWaiter = User.IsInRole(AppRoles.Waiter);

            var query = _context.RestaurantTables.AsQueryable();
            if (isWaiter)
            {
                query = query.Where(t => t.WaiterId == userId);
            }

            var tables = await query.OrderBy(t => t.TableNumber).ToListAsync();
            var activeSessions = await _context.Set<TableSession>()
                .Where(s => s.IsActive)
                .ToListAsync();
            var activeOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.AddOns)
                        .ThenInclude(a => a.AddOn)
                .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Initiated)
                .ToListAsync();

            var result = tables.Select(table =>
            {
                var session = activeSessions.FirstOrDefault(s => s.TableId == table.Id);
                var tableOrders = activeOrders.Where(o => o.TableNumber == table.TableNumber).ToList();
                var status = "Empty";
                if (tableOrders.Any())
                {
                    if (tableOrders.Any(o => o.Status == OrderStatus.Pending)) status = "Pending";
                    else status = "Active";
                }

                return new
                {
                    tableNumber = table.TableNumber,
                    status = status,
                    sessionId = session?.Id,
                    customerName = session?.CustomerName,
                    phoneNumber = session?.PhoneNumber,
                    orders = tableOrders.Select(o => new 
                    {
                        id = o.Id,
                        // Effective total for display (excludes cancelled items)
                        totalAmount = o.OrderItems.Where(oi => !oi.IsCancelled).Sum(oi => oi.EffectiveTotal),
                        orderTime = o.OrderDate,
                        note = o.Note,
                        status = o.Status.ToString(),
                        items = o.OrderItems.Select(oi => new {
                            id = oi.Id,
                            name = oi.MenuItem?.Name ?? "Unknown",
                            quantity = oi.Quantity,
                            isCancelled = oi.IsCancelled,
                            addOns = oi.AddOns.Select(a => new { name = a.AddOn?.Name ?? "" }).ToList()
                        }).ToList()
                    }).ToList()
                };
            })
            .OrderByDescending(t => t.sessionId.HasValue)
            .ThenBy(t => t.tableNumber)
            .ToList();

            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> CloseSession(int sessionId)
        {
            var session = await _context.Set<TableSession>()
                .Include(s => s.Table)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
                
            if (session != null)
            {
                session.IsActive = false;
                session.EndTime = DateTime.Now;
                
                // Mark all active orders for this session as completed
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
                
                // Notify dashboards to refresh
                await _hubContext.Clients.Group("waiter").SendAsync("TableCleared", notifyData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderCompleted", notifyData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", notifyData);
                
                return Ok();
            }
            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> AcceptOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null && order.Status == OrderStatus.Pending)
            {
                order.Status = OrderStatus.Confirmed;
                order.ConfirmedDate = DateTime.Now;
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
                await _hubContext.Clients.Group("waiter").SendAsync("OrderStatusChanged", orderData);
                await _hubContext.Clients.Group("chef").SendAsync("OrderAccepted", orderData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderAccepted", orderData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
                await _hubContext.Clients.All.SendAsync("OrderStatusChanged", orderData);

                // Legacy events for backward compatibility
                await _hubContext.Clients.All.SendAsync("ReceiveWaiterUpdate", "Order Accepted");
                await _hubContext.Clients.All.SendAsync("ReceiveChefUpdate", "New Order to Prepare");
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null && order.Status != OrderStatus.Completed)
            {
                order.Status = OrderStatus.Cancelled;
                order.CancelledDate = DateTime.Now;
                await _context.SaveChangesAsync();

                var orderData = new
                {
                    id = order.Id,
                    tableNumber = order.TableNumber,
                    status = (int)order.Status,
                    totalAmount = order.TotalAmount
                };

                // Notify all dashboards and guest tracking - table is now empty
                await _hubContext.Clients.Group("waiter").SendAsync("TableCleared", orderData);
                await _hubContext.Clients.Group("chef").SendAsync("OrderCancelled", orderData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderCompleted", orderData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
                await _hubContext.Clients.All.SendAsync("OrderStatusChanged", orderData);

                await _hubContext.Clients.All.SendAsync("ReceiveWaiterUpdate", "Order Cancelled");
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CompleteOrder(int orderId)
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

                // Notify all dashboards and guest tracking
                await _hubContext.Clients.Group("waiter").SendAsync("TableCleared", orderData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderCompleted", orderData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
                await _hubContext.Clients.All.SendAsync("OrderStatusChanged", orderData);

                // Legacy events
                await _hubContext.Clients.All.SendAsync("ReceiveWaiterUpdate", "Table Cleared");
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrderItem(int orderItemId)
        {
            var orderItem = await _context.OrderItems
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi2 => oi2.MenuItem)
                .FirstOrDefaultAsync(oi => oi.Id == orderItemId);

            if (orderItem == null) return NotFound();

            // Mark item as cancelled (soft delete)
            orderItem.IsCancelled = true;

            var order = orderItem.Order;
            if (order != null)
            {
                // Recalculate Subtotal from remaining active items
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
                orderId = order.Id,
                tableNumber = order.TableNumber,
                cancelledItemId = orderItemId,
                cancelledItemName = orderItem.MenuItem?.Name ?? "Item",
                orderItems = order.OrderItems.Select(oi => new
                {
                    id = oi.Id,
                    menuItemName = oi.MenuItem?.Name ?? "",
                    quantity = oi.Quantity,
                    price = oi.Price,
                    isCancelled = oi.IsCancelled
                })
            };

            // Notify chef: remove this item from view
            await _hubContext.Clients.Group("chef").SendAsync("OrderItemCancelled", itemCancelledData);
            // Notify cashier: show item in red (don't remove)
            await _hubContext.Clients.Group("cashier").SendAsync("OrderItemCancelled", itemCancelledData);
            // Notify all
            await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", new { tableNumber = order.TableNumber });
            await _hubContext.Clients.All.SendAsync("OrderItemCancelled", itemCancelledData);

            return Ok(new { message = $"Item cancelled: {orderItem.MenuItem?.Name}" });
        }

        [HttpPost]
        public async Task<IActionResult> ServeOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null && order.Status == OrderStatus.Ready)
            {
                order.Status = OrderStatus.Served;
                await _context.SaveChangesAsync();

                var orderData = new
                {
                    id = order.Id,
                    tableNumber = order.TableNumber,
                    status = order.Status.ToString(),
                    statusInt = (int)order.Status,
                    totalAmount = order.TotalAmount,
                    orderDate = order.OrderDate
                };

                // Notify all dashboards and clients via SignalR
                await _hubContext.Clients.Group("waiter").SendAsync("OrderStatusChanged", orderData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderStatusChanged", orderData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
                await _hubContext.Clients.All.SendAsync("OrderStatusChanged", orderData); // Broadcast directly to guest tracker in real-time
                await _hubContext.Clients.All.SendAsync("ReceiveWaiterUpdate", "Order Served");
            }
            return Ok();
        }
    }
}
