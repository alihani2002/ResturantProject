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

        public WaiterController(AppDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index()
        {
            var tables = await _context.RestaurantTables.OrderBy(t => t.TableNumber).ToListAsync();
            var activeOrders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            var viewModels = new List<WaiterTableViewModel>();

            foreach (var table in tables)
            {
                var tableOrders = activeOrders.Where(o => o.TableNumber == table.TableNumber).ToList();
                var viewModel = new WaiterTableViewModel
                {
                    TableId = table.Id,
                    TableNumber = table.TableNumber,
                    Orders = tableOrders.Select(o => new WaiterOrderViewModel 
                    {
                        OrderId = o.Id,
                        TotalAmount = o.TotalAmount,
                        OrderTime = o.OrderDate,
                        Note = o.Note,
                        Status = o.Status == OrderStatus.Pending ? "Pending" : "Active",
                        OrderItems = o.OrderItems.Select(oi => new WaiterOrderItemViewModel
                        {
                            Name = oi.MenuItem?.Name ?? "Unknown",
                            Quantity = oi.Quantity
                        }).ToList()
                    }).ToList()
                };

                viewModels.Add(viewModel);
            }

            return View(viewModels);
        }

        [HttpGet]
        public async Task<IActionResult> GetTablesData()
        {
            var tables = await _context.RestaurantTables.OrderBy(t => t.TableNumber).ToListAsync();
            var activeOrders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            var result = tables.Select(table =>
            {
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
                    orders = tableOrders.Select(o => new 
                    {
                        id = o.Id,
                        totalAmount = o.TotalAmount,
                        orderTime = o.OrderDate,
                        note = o.Note,
                        status = o.Status == OrderStatus.Pending ? "Pending" : "Active",
                        items = o.OrderItems.Select(oi => new {
                            name = oi.MenuItem?.Name ?? "Unknown",
                            quantity = oi.Quantity
                        }).ToList()
                    }).ToList()
                };
            });

            return Json(result);
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

                // Notify all dashboards
                await _hubContext.Clients.Group("waiter").SendAsync("OrderStatusChanged", orderData);
                await _hubContext.Clients.Group("chef").SendAsync("OrderAccepted", orderData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderAccepted", orderData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);

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

                // Notify all dashboards - table is now empty
                await _hubContext.Clients.Group("waiter").SendAsync("TableCleared", orderData);
                await _hubContext.Clients.Group("chef").SendAsync("OrderCancelled", orderData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderCompleted", orderData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);

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

                // Notify all dashboards
                await _hubContext.Clients.Group("waiter").SendAsync("TableCleared", orderData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderCompleted", orderData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);

                // Legacy events
                await _hubContext.Clients.All.SendAsync("ReceiveWaiterUpdate", "Table Cleared");
            }
            return Ok();
        }
    }
}