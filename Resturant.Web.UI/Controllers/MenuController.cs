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
            if (tableNumber == null)
            {
                string tableNumberStr = Request.Cookies["TableNumber"];
                if (int.TryParse(tableNumberStr, out int cachedTableNumber))
                {
                    tableNumber = cachedTableNumber;
                }
            }

            if (tableNumber == null)
            {
                // No table number found, redirect to a landing page or show error
                return RedirectToAction("Index", "Home");
            }

            // Check for active session
            var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.TableNumber == tableNumber);
            if (table == null) return NotFound("Table not found");

            var activeSession = await _context.Set<TableSession>().FirstOrDefaultAsync(s => s.TableId == table.Id && s.IsActive);
            if (activeSession == null)
            {
                // No active session for this table, redirect to registration
                return RedirectToAction("Register", "CustomerSession");
            }

            var menu = await _context.MenuCategories
                .Include(c => c.MenuItems.Where(i => i.IsAvailable).OrderBy(i => i.OrderNumber).ThenBy(i => i.Name))
                .Where(c => c.IsActive)
                .OrderBy(c => c.OrderNumber)
                .ThenBy(c => c.Name)
                .ToListAsync();

            ViewData["TableNumber"] = tableNumber;
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
            var categories = await _context.MenuCategories
                .Where(c => c.IsActive)
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