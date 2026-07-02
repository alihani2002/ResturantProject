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
            else if (roles.Contains(AppRoles.Driver))
            {
                return RedirectToAction("Index", "Driver");
            }
            else if (roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.Manager))
            {
                // Admin and Manager can access all dashboards, show admin dashboard
                ViewBag.Branches = await _context.Branches.Where(b => !b.IsDeleted && b.IsActive).ToListAsync();
                ViewBag.ActiveBranchId = Request.Cookies["AdminActiveBranchId"];
                return View();
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SwitchBranch(int? branchId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (user.BranchId.HasValue)
            {
                return BadRequest(new { success = false, message = "You are not authorized to switch branches." });
            }

            CookieOptions option = new CookieOptions
            {
                Expires = DateTime.Now.AddYears(1),
                HttpOnly = true,
                Secure = true
            };

            if (branchId.HasValue && branchId.Value > 0)
            {
                Response.Cookies.Append("AdminActiveBranchId", branchId.Value.ToString(), option);
            }
            else
            {
                Response.Cookies.Delete("AdminActiveBranchId");
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var today = DateTime.Today;
            string activeBranchIdStr = Request.Cookies["AdminActiveBranchId"];
            int? activeBranchId = null;
            if (!string.IsNullOrEmpty(activeBranchIdStr) && int.TryParse(activeBranchIdStr, out int parsedId))
            {
                activeBranchId = parsedId;
            }

            var ordersQuery = _context.Orders.AsQueryable();
            if (activeBranchId.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.BranchId == activeBranchId.Value);
            }

            var activeTables = await ordersQuery
                .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
                .Select(o => o.TableNumber)
                .Distinct()
                .CountAsync();

            var pendingOrders = await ordersQuery
                .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.InPreparation)
                .CountAsync();

            var completedToday = await ordersQuery
                .Where(o => o.Status == OrderStatus.Completed && o.CompletedDate.HasValue && o.CompletedDate.Value.Date == today)
                .CountAsync();

            var totalRevenueToday = await ordersQuery
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
            string activeBranchIdStr = Request.Cookies["AdminActiveBranchId"];
            int? activeBranchId = null;
            if (!string.IsNullOrEmpty(activeBranchIdStr) && int.TryParse(activeBranchIdStr, out int parsedId))
            {
                activeBranchId = parsedId;
            }

            var tablesQuery = _context.RestaurantTables.AsQueryable();
            if (activeBranchId.HasValue)
            {
                tablesQuery = tablesQuery.Where(t => t.BranchId == activeBranchId.Value);
            }

            var tables = await tablesQuery
                .Select(t => new {
                    id = t.Id,
                    tableNumber = t.TableNumber,
                    waiterId = t.WaiterId
                })
                .OrderBy(t => t.tableNumber)
                .ToListAsync();

            var waiters = await _userManager.GetUsersInRoleAsync(AppRoles.Waiter);
            var waiterList = waiters
                .Where(w => !activeBranchId.HasValue || w.BranchId == activeBranchId.Value)
                .Select(w => new { id = w.Id, fullName = w.FullName ?? w.UserName })
                .ToList();

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

        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            string activeBranchIdStr = Request.Cookies["AdminActiveBranchId"];
            int branchId = 1;
            if (!string.IsNullOrEmpty(activeBranchIdStr) && int.TryParse(activeBranchIdStr, out int parsedId))
            {
                branchId = parsedId;
            }

            var settings = await _context.RestaurantSettings.FirstOrDefaultAsync(s => s.BranchId == branchId);
            if (settings == null)
            {
                settings = new RestaurantSetting
                {
                    TaxPercentage = 14,
                    ServicePercentage = 12,
                    CreatedOn = System.DateTime.Now,
                    BranchId = branchId
                };
                _context.RestaurantSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return View(settings);
        }

        [HttpPost]
        public async Task<IActionResult> Settings(RestaurantSetting model)
        {
            string activeBranchIdStr = Request.Cookies["AdminActiveBranchId"];
            int branchId = 1;
            if (!string.IsNullOrEmpty(activeBranchIdStr) && int.TryParse(activeBranchIdStr, out int parsedId))
            {
                branchId = parsedId;
            }

            if (ModelState.IsValid)
            {
                var settings = await _context.RestaurantSettings.FirstOrDefaultAsync(s => s.BranchId == branchId);
                if (settings == null)
                {
                    settings = new RestaurantSetting { CreatedOn = System.DateTime.Now, BranchId = branchId };
                    _context.RestaurantSettings.Add(settings);
                }

                settings.TaxPercentage = model.TaxPercentage;
                settings.ServicePercentage = model.ServicePercentage;
                settings.LastUpdatedOn = System.DateTime.Now;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث إعدادات الضرائب والخدمة بنجاح!";
                return RedirectToAction(nameof(Settings));
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            string activeBranchIdStr = Request.Cookies["AdminActiveBranchId"];
            int branchId = 1;
            if (!string.IsNullOrEmpty(activeBranchIdStr) && int.TryParse(activeBranchIdStr, out int parsedId))
            {
                branchId = parsedId;
            }

            var settings = await _context.RestaurantSettings.FirstOrDefaultAsync(s => s.BranchId == branchId);
            if (settings == null)
            {
                settings = new RestaurantSetting
                {
                    TaxPercentage = 14,
                    ServicePercentage = 12,
                    CreatedOn = System.DateTime.Now,
                    BranchId = branchId
                };
                _context.RestaurantSettings.Add(settings);
                await _context.SaveChangesAsync();
            }
            return Json(new { taxPercentage = settings.TaxPercentage, servicePercentage = settings.ServicePercentage });
        }

        [HttpPost]
        public async Task<IActionResult> SaveSettings(decimal taxPercentage, decimal servicePercentage)
        {
            string activeBranchIdStr = Request.Cookies["AdminActiveBranchId"];
            int branchId = 1;
            if (!string.IsNullOrEmpty(activeBranchIdStr) && int.TryParse(activeBranchIdStr, out int parsedId))
            {
                branchId = parsedId;
            }

            var settings = await _context.RestaurantSettings.FirstOrDefaultAsync(s => s.BranchId == branchId);
            if (settings == null)
            {
                settings = new RestaurantSetting { CreatedOn = System.DateTime.Now, BranchId = branchId };
                _context.RestaurantSettings.Add(settings);
            }

            settings.TaxPercentage = taxPercentage;
            settings.ServicePercentage = servicePercentage;
            settings.LastUpdatedOn = System.DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> DeliveryZones()
        {
            string activeBranchIdStr = Request.Cookies["AdminActiveBranchId"];
            int branchId = 1;
            if (!string.IsNullOrEmpty(activeBranchIdStr) && int.TryParse(activeBranchIdStr, out int parsedId))
            {
                branchId = parsedId;
            }

            var zones = await _context.DeliveryZones
                .Where(z => z.BranchId == branchId && !z.IsDeleted)
                .ToListAsync();

            // Load drivers for this branch
            var drivers = await _context.Set<Driver>()
                .Include(d => d.Branch)
                .Where(d => d.BranchId == branchId && !d.IsDeleted)
                .ToListAsync();

            // Load delivery orders for this branch
            var orders = await _context.Orders
                .Include(o => o.Driver)
                .Include(o => o.DeliveryAddress)
                .Where(o => o.BranchId == branchId && o.OrderType == OrderType.Delivery)
                .OrderByDescending(o => o.OrderDate)
                .Take(100)
                .ToListAsync();

            ViewBag.BranchId = branchId;
            ViewBag.Drivers = drivers;
            ViewBag.Orders = orders;
            ViewBag.AllBranches = await _context.Branches.Where(b => !b.IsDeleted && b.IsActive).ToListAsync();
            return View(zones);
        }

        [HttpPost]
        public async Task<IActionResult> CreateDeliveryZone(string name, string governorate, decimal deliveryFee, string iframe)
        {
            string activeBranchIdStr = Request.Cookies["AdminActiveBranchId"];
            int branchId = 1;
            if (!string.IsNullOrEmpty(activeBranchIdStr) && int.TryParse(activeBranchIdStr, out int parsedId))
            {
                branchId = parsedId;
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(governorate))
            {
                TempData["ErrorMessage"] = "جميع الحقول مطلوبة!";
                return RedirectToAction(nameof(DeliveryZones), new { iframe = iframe == "true" ? "true" : null });
            }

            var zone = new DeliveryZone
            {
                Name = name,
                ArabicName = name,
                Governorate = governorate,
                ArabicGovernorate = governorate,
                DeliveryFee = deliveryFee,
                IsActive = true,
                BranchId = branchId,
                CreatedOn = System.DateTime.Now
            };

            _context.DeliveryZones.Add(zone);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة منطقة التوصيل بنجاح!";
            return RedirectToAction(nameof(DeliveryZones), new { iframe = iframe == "true" ? "true" : null });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDeliveryZone(int id, string iframe)
        {
            var zone = await _context.DeliveryZones.FindAsync(id);
            if (zone != null)
            {
                zone.IsDeleted = true;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف المنطقة بنجاح!";
            }
            return RedirectToAction(nameof(DeliveryZones), new { iframe = iframe == "true" ? "true" : null });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDriver(int id, string fullName, string phoneNumber, string vehicleNumber, int branchId, string iframe)
        {
            var driver = await _context.Set<Driver>().FindAsync(id);
            if (driver != null)
            {
                driver.FullName = fullName;
                driver.PhoneNumber = phoneNumber;
                driver.VehicleNumber = vehicleNumber;
                driver.BranchId = branchId;

                var user = await _userManager.FindByIdAsync(driver.UserId);
                if (user != null)
                {
                    user.FullName = fullName;
                    user.PhoneNumber = phoneNumber;
                    user.BranchId = branchId;
                    await _userManager.UpdateAsync(user);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تحديث بيانات الطيار بنجاح!";
            }
            else
            {
                TempData["ErrorMessage"] = "عذراً، لم يتم العثور على الطيار.";
            }

            return RedirectToAction(nameof(DeliveryZones), new { iframe = iframe == "true" ? "true" : null });
        }
    }
}

