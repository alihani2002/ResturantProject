/* 
 * NOTE: didn't create migration or database
 */
using Microsoft.AspNetCore.Mvc;
using Resturant.Infrastructure.Data;
using Resturant.Core.Entities;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Resturant.Web.UI.Controllers
{
    public class MenuController : Controller
    {
        private readonly AppDbContext _context;

        public MenuController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Menu
        public async Task<IActionResult> Index(int? tableNumber, int? categoryId)
        {
            string tableNumberStr = Request.Cookies["TableNumber"];
            string branchIdStr = Request.Cookies["BranchId"];
            if (string.IsNullOrEmpty(tableNumberStr) || string.IsNullOrEmpty(branchIdStr))
            {
                return RedirectToAction("Index", "Home");
            }

            if (tableNumber == null)
            {
                if (int.TryParse(tableNumberStr, out int cachedTableNumber))
                {
                    tableNumber = cachedTableNumber;
                }
            }

            if (tableNumber == null || !int.TryParse(branchIdStr, out int branchId))
            {
                return RedirectToAction("Index", "Home");
            }

            // Check for active session for this table in this branch
            var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.TableNumber == tableNumber && t.BranchId == branchId);
            if (table == null) return NotFound("Table not found");

            var activeSession = await _context.Set<TableSession>().FirstOrDefaultAsync(s => s.TableId == table.Id && s.IsActive);
            if (activeSession == null)
            {
                return RedirectToAction("Register", "CustomerSession");
            }
            else
            {
                // Table is occupied, verify identity using cookies
                string guestName = Request.Cookies["GuestName"];
                string guestPhone = Request.Cookies["GuestPhone"];

                if (string.IsNullOrEmpty(guestName) || 
                    string.IsNullOrEmpty(guestPhone) || 
                    !activeSession.CustomerName.Equals(guestName, StringComparison.OrdinalIgnoreCase) || 
                    activeSession.PhoneNumber != guestPhone)
                {
                    return RedirectToAction("Register", "CustomerSession");
                }
            }

            // Filter categories and menu items by the table's BranchId
            var menu = await _context.MenuCategories
                .Include(c => c.MenuItems.Where(i => i.IsAvailable && i.BranchId == branchId).OrderBy(i => i.OrderNumber).ThenBy(i => i.Name))
                .Where(c => c.IsActive && c.BranchId == branchId)
                .OrderBy(c => c.OrderNumber)
                .ThenBy(c => c.Name)
                .ToListAsync();

            ViewData["TableNumber"] = tableNumber;
            ViewData["BranchId"] = branchId;
            ViewData["CustomerName"] = activeSession.CustomerName;
            ViewData["SelectedCategoryId"] = categoryId;
            return View(menu);
        }

        // GET: Menu/GetMenuItemsByCategory
        [HttpGet]
        public async Task<IActionResult> GetMenuItemsByCategory(int categoryId)
        {
            var menuItems = await _context.MenuItems
                .Where(i => i.MenuCategoryId == categoryId && i.IsAvailable)
                .OrderBy(i => i.OrderNumber)
                .ThenBy(i => i.Name)
                .Select(i => new 
                {
                    id = i.Id,
                    name = i.Name,
                    description = i.Description,
                    price = i.Price,
                    imageUrl = i.ImageUrl,
                    isAvailable = i.IsAvailable,
                    menuCategoryId = i.MenuCategoryId,
                    isPopular = i.IsPopular,
                    isTrending = i.IsTrending,
                    isRecommended = i.IsRecommended,
                    orderNumber = i.OrderNumber
                })
                .ToListAsync();

            return Json(menuItems);
        }

        // GET: Menu/GetMenuItemCustomizationDetails
        [HttpGet]
        public async Task<IActionResult> GetMenuItemCustomizationDetails(int id)
        {
            var menuItem = await _context.MenuItems
                .Include(m => m.AddOns)
                .Include(m => m.Recommendations)
                    .ThenInclude(r => r.RecommendedMenuItem)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (menuItem == null)
            {
                return NotFound();
            }

            var result = new
            {
                id = menuItem.Id,
                name = menuItem.Name,
                description = menuItem.Description,
                price = menuItem.Price,
                imageUrl = menuItem.ImageUrl,
                sizes = menuItem.GetParsedSizes().Select(s => new { name = s.Name, price = s.Price }).ToList(),
                addOns = menuItem.AddOns
                    .Where(a => a.IsAvailable)
                    .Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        extraPrice = a.ExtraPrice
                    }).ToList(),
                recommendations = menuItem.Recommendations
                    .Where(r => r.RecommendedMenuItem != null && r.RecommendedMenuItem.IsAvailable)
                    .Select(r => new
                    {
                        id = r.RecommendedMenuItem.Id,
                        name = r.RecommendedMenuItem.Name,
                        description = r.RecommendedMenuItem.Description,
                        price = r.RecommendedMenuItem.Price,
                        imageUrl = r.RecommendedMenuItem.ImageUrl
                    }).ToList()
            };

            return Json(result);
        }

        // GET: Menu/GetAllCategories
        [HttpGet]
        public async Task<IActionResult> GetAllCategories()
        {
            string branchIdStr = Request.Cookies["BranchId"];
            int branchId = 1;
            if (!string.IsNullOrEmpty(branchIdStr))
            {
                int.TryParse(branchIdStr, out branchId);
            }

            var categories = await _context.MenuCategories
                .Where(c => c.IsActive && c.BranchId == branchId)
                .OrderBy(c => c.OrderNumber)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return Json(categories);
        }

        // GET: Menu/GetMenuItemDetails
        [HttpGet]
        public async Task<IActionResult> GetMenuItemDetails(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return NotFound();
            }

            return Json(menuItem);
        }
    }
}