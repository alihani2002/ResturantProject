using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using Resturant.Web.UI.Hubs;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Driver)]
    public class DriverController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<OrderHub> _hubContext;

        public DriverController(AppDbContext context, UserManager<ApplicationUser> userManager, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        // GET: /Driver
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var driver = await _context.Set<Driver>().FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
            if (driver == null)
            {
                return Content("Driver profile not found.");
            }

            var activeOrders = await _context.Orders
                .Include(o => o.DeliveryAddress)
                .Where(o => o.DriverId == driver.Id && 
                            (o.Status == OrderStatus.OutForDelivery || o.Status == OrderStatus.Ready))
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.Driver = driver;
            return View(activeOrders);
        }

        // POST: /Driver/StartTrip
        [HttpPost]
        public async Task<IActionResult> StartTrip(int orderId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var driver = await _context.Set<Driver>().FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
            if (driver == null) return NotFound("Driver not found");

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.DriverId == driver.Id);
            if (order == null) return NotFound("Order not found");

            order.Status = OrderStatus.OutForDelivery;
            driver.Status = DriverStatus.OnDelivery;
            await _context.SaveChangesAsync();

            // Notify Customer & Cashier
            await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusChanged", new { orderId = order.Id, status = OrderStatus.OutForDelivery.ToString() });
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.OutForDelivery.ToString() });

            TempData["SuccessMessage"] = "تم بدء الرحلة بنجاح! تم تنبيه العميل بأن الطلب في الطريق.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Driver/CompleteOrder
        [HttpPost]
        public async Task<IActionResult> CompleteOrder(int orderId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var driver = await _context.Set<Driver>().FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
            if (driver == null) return NotFound("Driver not found");

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.DriverId == driver.Id);
            if (order == null) return NotFound("Order not found");

            order.Status = OrderStatus.Delivered;
            driver.Status = DriverStatus.Idle;
            await _context.SaveChangesAsync();

            // Notify Customer & Cashier
            await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusChanged", new { orderId = order.Id, status = OrderStatus.Delivered.ToString() });
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.Delivered.ToString() });

            TempData["SuccessMessage"] = "تم توصيل الطلب بنجاح! يرجى تسوية الحساب مع الكاشير لتأكيد التحصيل.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Driver/FailOrder
        [HttpPost]
        public async Task<IActionResult> FailOrder(int orderId, string reason)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var driver = await _context.Set<Driver>().FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
            if (driver == null) return NotFound("Driver not found");

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.DriverId == driver.Id);
            if (order == null) return NotFound("Order not found");

            order.Status = OrderStatus.FailedDelivery;
            order.Note = $"{order.Note} | فشل التوصيل: {reason}";
            driver.Status = DriverStatus.Idle;
            await _context.SaveChangesAsync();

            // Notify Customer & Cashier
            await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusChanged", new { orderId = order.Id, status = OrderStatus.FailedDelivery.ToString(), failureReason = reason });
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.FailedDelivery.ToString() });

            TempData["ErrorMessage"] = "تم تسجيل فشل التوصيل بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Driver/History
        public async Task<IActionResult> History()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var driver = await _context.Set<Driver>().FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
            if (driver == null) return Content("Driver profile not found.");

            var historyOrders = await _context.Orders
                .Include(o => o.DeliveryAddress)
                .Where(o => o.DriverId == driver.Id && 
                            (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid || o.Status == OrderStatus.FailedDelivery || o.Status == OrderStatus.Delivered))
                .OrderByDescending(o => o.OrderDate)
                .Take(50)
                .ToListAsync();

            ViewBag.Driver = driver;
            return View(historyOrders);
        }
    }
}
