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
            var menu = await _context.MenuCategories
                .Include(c => c.MenuItems.Where(i => i.IsAvailable))
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewData["TableNumber"] = tableNumber;
            ViewData["SelectedCategoryId"] = categoryId;
            return View(menu);
        }

        // GET: Menu/GetMenuItemsByCategory
        [HttpGet]
        public async Task<IActionResult> GetMenuItemsByCategory(int categoryId)
        {
            var menuItems = await _context.MenuItems
                .Where(i => i.MenuCategoryId == categoryId && i.IsAvailable)
                .OrderBy(i => i.Name)
                .Select(i => new 
                {
                    id = i.Id,
                    name = i.Name,
                    description = i.Description,
                    price = i.Price,
                    imageUrl = i.ImageUrl,
                    isAvailable = i.IsAvailable,
                    menuCategoryId = i.MenuCategoryId
                })
                .ToListAsync();

            return Json(menuItems);
        }

        // GET: Menu/GetAllCategories
        [HttpGet]
        public async Task<IActionResult> GetAllCategories()
        {
            var categories = await _context.MenuCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
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