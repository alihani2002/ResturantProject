using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    // Allow any role except specific 'User' role, but practically we whitelist allowed roles
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Chief + "," + AppRoles.Waiter + "," + AppRoles.Manager + "," + AppRoles.Accountant)]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;

        public AdminController(UserManager<ApplicationUser> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            // Redirect to appropriate dashboard based on role
            if (roles.Contains(AppRoles.Waiter))
            {
                return RedirectToAction("Index", "Waiter");
            }
            else if (roles.Contains(AppRoles.Chief))
            {
                return RedirectToAction("Index", "Chef");
            }
            else if (roles.Contains(AppRoles.Accountant))
            {
                return RedirectToAction("Index", "Cashier");
            }
            else if (roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.Manager))
            {
                // Admin and Manager can access all dashboards, show admin dashboard
                return View();
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var today = DateTime.Today;

            var activeTables = await _context.Orders
                .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
                .Select(o => o.TableNumber)
                .Distinct()
                .CountAsync();

            var pendingOrders = await _context.Orders
                .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.InPreparation)
                .CountAsync();

            var completedToday = await _context.Orders
                .Where(o => o.Status == OrderStatus.Completed && o.CompletedDate.HasValue && o.CompletedDate.Value.Date == today)
                .CountAsync();

            var totalRevenueToday = await _context.Orders
                .Where(o => o.Status == OrderStatus.Completed && o.CompletedDate.HasValue && o.CompletedDate.Value.Date == today)
                .SumAsync(o => o.TotalAmount);

            return Json(new
            {
                activeTables,
                pendingOrders,
                completedToday,
                totalRevenueToday
            });
        }
        [HttpGet]
        public async Task<IActionResult> GetWaiterAssignments()
        {
            var tables = await _context.RestaurantTables
                .Select(t => new {
                    id = t.Id,
                    tableNumber = t.TableNumber,
                    waiterId = t.WaiterId
                })
                .OrderBy(t => t.tableNumber)
                .ToListAsync();

            var waiters = await _userManager.GetUsersInRoleAsync(AppRoles.Waiter);
            var waiterList = waiters.Select(w => new { id = w.Id, fullName = w.FullName ?? w.UserName }).ToList();

            return Json(new { tables, waiters = waiterList });
        }

        [HttpPost]
        public async Task<IActionResult> AssignWaiter(int tableId, string? waiterId)
        {
            var table = await _context.RestaurantTables.FindAsync(tableId);
            if (table == null) return Json(new { success = false, message = "Table not found." });

            table.WaiterId = string.IsNullOrEmpty(waiterId) ? null : waiterId;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
