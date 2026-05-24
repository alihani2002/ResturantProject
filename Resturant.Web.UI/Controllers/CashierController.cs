using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using Resturant.Web.UI.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Accountant + "," + AppRoles.Admin + "," + AppRoles.Manager)]
    public class CashierController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;

        public CashierController(AppDbContext context, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var activeShift = await _context.Set<CashierShift>()
                .FirstOrDefaultAsync(s => s.IsActive && s.CashierId == userId);

            if (activeShift == null)
            {
                ViewBag.NoActiveShift = true;
                return View(new List<Order>());
            }

            var activeOrders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Ready ||
                            o.Status == OrderStatus.InPreparation || o.Status == OrderStatus.Served ||
                            o.Status == OrderStatus.Cancelled)
                .OrderBy(o => o.OrderDate)
                .ToListAsync();

            ViewBag.ActiveShift = activeShift;
            return View(activeOrders);
        }

        [HttpPost]
        public async Task<IActionResult> StartShift()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var activeShift = await _context.Set<CashierShift>()
                .FirstOrDefaultAsync(s => s.IsActive && s.CashierId == userId);

            if (activeShift == null)
            {
                var shift = new CashierShift
                {
                    CashierId = userId,
                    StartTime = DateTime.Now,
                    IsActive = true,
                    CreatedOn = DateTime.Now,
                    ExpectedAmount = 0
                };
                _context.Set<CashierShift>().Add(shift);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CloseShift(decimal actualAmount)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var activeShift = await _context.Set<CashierShift>()
                .FirstOrDefaultAsync(s => s.IsActive && s.CashierId == userId);

            if (activeShift == null)
            {
                return BadRequest("No active shift found.");
            }

            // Calculate Expected Amount based on completed orders
            var shiftOrders = await _context.Orders
                .Where(o => o.ShiftId == activeShift.Id && o.Status == OrderStatus.Completed)
                .ToListAsync();

            decimal expectedAmount = shiftOrders.Sum(o => (o.PaidAmount ?? o.TotalAmount) - (o.ChangeReturned ?? 0));

            activeShift.ExpectedAmount = expectedAmount;
            activeShift.ActualAmountEntered = actualAmount;
            activeShift.Difference = actualAmount - expectedAmount;
            activeShift.IsActive = false;
            activeShift.EndTime = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ShiftSummary), new { id = activeShift.Id });
        }

        public async Task<IActionResult> ShiftSummary(int id)
        {
            var shift = await _context.Set<CashierShift>()
                .Include(s => s.Cashier)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shift == null) return NotFound();

            // Fetch shift orders
            var shiftOrders = await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.ShiftId == id && o.Status == OrderStatus.Completed)
                .ToListAsync();

            ViewBag.ShiftOrdersCount = shiftOrders.Count;
            ViewBag.ShiftTotalSales = shiftOrders.Sum(o => o.TotalAmount);
            ViewBag.ShiftTotalTips = shiftOrders.Sum(o => o.Tips ?? 0);

            return View(shift);
        }

        public async Task<IActionResult> ShiftHistory()
        {
            var shifts = await _context.Set<CashierShift>()
                .Include(s => s.Cashier)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();

            return View(shifts);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrdersData()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Ready ||
                            o.Status == OrderStatus.InPreparation || o.Status == OrderStatus.Served ||
                            o.Status == OrderStatus.Cancelled)
                .ToListAsync();

            var groupedOrders = orders.GroupBy(o => o.TableSessionId ?? -o.TableNumber)
                .Select(g => new
                {
                    sessionId = g.Key > 0 ? g.Key : (int?)null,
                    tableNumber = g.First().TableNumber,
                    customerName = g.First().CustomerName ?? "Guest",
                    phoneNumber = g.First().PhoneNumber ?? "N/A",
                    isCancelled = g.All(o => o.Status == OrderStatus.Cancelled),
                    status = g.Any(o => o.Status == OrderStatus.Ready) ? (int)OrderStatus.Ready : (int)g.First().Status,
                    totalAmount = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount),
                    orderIds = g.Select(o => o.Id).ToList(),
                    orderItems = g.SelectMany(o => o.OrderItems).Select(oi => new
                    {
                        id = oi.Id,
                        menuItemName = oi.MenuItem != null ? oi.MenuItem.Name : "",
                        quantity = oi.Quantity,
                        price = oi.Price,
                        isCancelled = oi.IsCancelled
                    })
                })
                .ToList();

            return Json(groupedOrders);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment(string orderIds, decimal paidAmount, decimal changeReturned, decimal tips)
        {
            if (string.IsNullOrEmpty(orderIds)) return BadRequest();

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var activeShift = await _context.Set<CashierShift>()
                .FirstOrDefaultAsync(s => s.IsActive && s.CashierId == userId);

            if (activeShift == null)
            {
                return BadRequest("You do not have an active shift. Please start your shift first.");
            }

            var ids = orderIds.Split(',').Select(int.Parse).ToList();
            var orders = await _context.Orders.Where(o => ids.Contains(o.Id)).ToListAsync();

            if (orders.Any())
            {
                int? sessionId = orders.First().TableSessionId;
                int tableNumber = orders.First().TableNumber;

                for (int i = 0; i < orders.Count; i++)
                {
                    var order = orders[i];
                    order.Status = OrderStatus.Completed;
                    order.CompletedDate = DateTime.Now;
                    order.ShiftId = activeShift.Id;
                    order.CashierId = userId;

                    if (i == 0)
                    {
                        order.PaidAmount = paidAmount;
                        order.ChangeReturned = changeReturned;
                        order.Tips = tips;
                    }
                    else
                    {
                        order.PaidAmount = order.TotalAmount;
                        order.ChangeReturned = 0;
                        order.Tips = 0;
                    }
                }

                if (sessionId.HasValue)
                {
                    var session = await _context.Set<TableSession>().FindAsync(sessionId.Value);
                    if (session != null)
                    {
                        session.IsActive = false;
                        session.EndTime = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                var notifyData = new { tableNumber = tableNumber };

                await _hubContext.Clients.Group("waiter").SendAsync("TableCleared", notifyData);
                await _hubContext.Clients.Group("cashier").SendAsync("OrderCompleted", notifyData);
                await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", notifyData);
                await _hubContext.Clients.All.SendAsync("OrderStatusChanged", new { tableNumber = tableNumber });

                return Ok();
            }
            return NotFound();
        }

        public async Task<IActionResult> PaidOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == OrderStatus.Completed)
                .OrderByDescending(o => o.CompletedDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> AllOrders(DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.OrderDate.Date == targetDate)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.SelectedDate = targetDate;
            return View(orders);
        }

        public async Task<IActionResult> Receipt(string ids)
        {
            if (string.IsNullOrEmpty(ids)) return NotFound();

            var orderIds = ids.Split(',').Select(int.Parse).ToList();
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.AddOns)
                        .ThenInclude(a => a.AddOn)
                .Where(o => orderIds.Contains(o.Id))
                .ToListAsync();

            if (!orders.Any()) return NotFound();

            bool hadCancellations = orders.Any(o => o.OrderItems.Any(oi => oi.IsCancelled));
            ViewBag.HadCancellations = hadCancellations;
            ViewBag.AllOrders = orders;

            return View(orders.First());
        }

        [HttpGet]
        public async Task<IActionResult> GetMenuData()
        {
            var menu = await _context.MenuCategories
                .Include(c => c.MenuItems)
                .ThenInclude(m => m.AddOns)
                .Where(c => c.IsActive && c.MenuItems.Any(mi => mi.IsAvailable))
                .OrderBy(c => c.OrderNumber)
                .Select(c => new
                {
                    categoryId = c.Id,
                    categoryName = c.Name,
                    items = c.MenuItems.Where(mi => mi.IsAvailable).Select(mi => new
                    {
                        id = mi.Id,
                        name = mi.Name,
                        price = mi.Price,
                        addOns = mi.AddOns.Where(a => a.IsAvailable).Select(a => new
                        {
                            id = a.Id,
                            name = a.Name,
                            price = a.ExtraPrice
                        }).ToList()
                    }).OrderBy(mi => mi.name).ToList()
                })
                .ToListAsync();

            return Json(menu);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTakeawayOrder([FromBody] CreateTakeawayOrderRequest request)
        {
            if (request == null || request.Items == null || !request.Items.Any())
            {
                return BadRequest("Invalid takeaway order data.");
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var activeShift = await _context.Set<CashierShift>()
                .FirstOrDefaultAsync(s => s.IsActive && s.CashierId == userId);

            if (activeShift == null)
            {
                return BadRequest("You do not have an active shift. Please start your shift first.");
            }

            // Create Order
            var order = new Order
            {
                TableNumber = 0, // 0 represents Takeaway
                CustomerName = "Takeaway - " + DateTime.Now.ToString("HH:mm"),
                PhoneNumber = "N/A",
                Status = OrderStatus.Completed,
                OrderDate = DateTime.Now,
                CompletedDate = DateTime.Now,
                ShiftId = activeShift.Id,
                CashierId = userId,
                PaidAmount = request.PaidAmount,
                ChangeReturned = request.ChangeReturned,
                Tips = request.Tips,
                Note = "Takeaway Order"
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            decimal totalAmount = 0;

            foreach (var itemReq in request.Items)
            {
                var menuItem = await _context.MenuItems.FindAsync(itemReq.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable) continue;

                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    MenuItemId = itemReq.MenuItemId,
                    Quantity = itemReq.Quantity,
                    Price = menuItem.Price,
                    IsCancelled = false
                };

                _context.OrderItems.Add(orderItem);

                decimal addOnsTotal = 0;
                if (itemReq.AddOnIds != null && itemReq.AddOnIds.Any())
                {
                    var addons = await _context.MenuItemAddOns
                        .Where(a => itemReq.AddOnIds.Contains(a.Id) && a.MenuItemId == itemReq.MenuItemId)
                        .ToListAsync();

                    foreach (var addon in addons)
                    {
                        var orderItemAddon = new OrderItemAddOn
                        {
                            OrderItem = orderItem,
                            MenuItemAddOnId = addon.Id,
                            Price = addon.ExtraPrice
                        };
                        _context.OrderItemAddOns.Add(orderItemAddon);
                        addOnsTotal += addon.ExtraPrice;
                    }
                }

                totalAmount += (menuItem.Price + addOnsTotal) * itemReq.Quantity;
            }

            var settings = await _context.RestaurantSettings.FirstOrDefaultAsync() 
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

            // Notify dashboards via SignalR
            var orderData = new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                status = order.Status.ToString(),
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate
            };
            await _hubContext.Clients.Group("cashier").SendAsync("OrderCompleted", orderData);
            await _hubContext.Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);

            return Ok(new { orderId = order.Id });
        }
    }

    public class CreateTakeawayOrderRequest
    {
        public List<TakeawayItemRequest> Items { get; set; } = new List<TakeawayItemRequest>();
        public decimal PaidAmount { get; set; }
        public decimal ChangeReturned { get; set; }
        public decimal Tips { get; set; }
    }

    public class TakeawayItemRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
        public List<int> AddOnIds { get; set; } = new List<int>();
    }
}