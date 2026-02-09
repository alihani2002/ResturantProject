using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using Resturant.Infrastructure.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Manager)]
    public class MenuItemController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public MenuItemController(AppDbContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        // GET: MenuItem
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.MenuItems.Include(m => m.MenuCategory);
            return View(await appDbContext.ToListAsync());
        }

        // GET: MenuItem/Create
        public IActionResult Create()
        {
            ViewBag.MenuCategories = _context.MenuCategories.ToList();
            ViewData["MenuCategoryId"] = new SelectList(_context.MenuCategories, "Id", "Name");
            return View();
        }

        // POST: MenuItem/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MenuItem menuItem, Microsoft.AspNetCore.Http.IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                // Upload image if provided
                if (imageFile != null && imageFile.Length > 0)
                {
                    menuItem.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                }

                _context.Add(menuItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.MenuCategories = _context.MenuCategories.ToList();
            ViewData["MenuCategoryId"] = new SelectList(_context.MenuCategories, "Id", "Name", menuItem.MenuCategoryId);
            return View(menuItem);
        }

        // GET: MenuItem/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem == null)
            {
                return NotFound();
            }
            ViewBag.MenuCategories = _context.MenuCategories.ToList();
            ViewData["MenuCategoryId"] = new SelectList(_context.MenuCategories, "Id", "Name", menuItem.MenuCategoryId);
            return View(menuItem);
        }

        // POST: MenuItem/Edit/5
        // POST: MenuItem/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MenuItem menuItem, Microsoft.AspNetCore.Http.IFormFile? imageFile)
        {
            if (id != menuItem.Id)
            {
                return NotFound();
            }

            // Fetch existing item to retrive the current image URL
            var existingMenuItem = await _context.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (existingMenuItem == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Upload new image if provided
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        menuItem.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                    }
                    else
                    {
                        // Keep the existing image
                        menuItem.ImageUrl = existingMenuItem.ImageUrl;
                    }

                    _context.Update(menuItem);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MenuItemExists(menuItem.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            
            // Repopulate ViewBag.MenuCategories for the dropdown if validation fails
            ViewBag.MenuCategories = _context.MenuCategories.ToList();
            ViewData["MenuCategoryId"] = new SelectList(_context.MenuCategories, "Id", "Name", menuItem.MenuCategoryId);
            return View(menuItem);
        }

        // POST: MenuItem/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var menuItem = await _context.MenuItems.FindAsync(id);
            if (menuItem != null)
            {
                _context.MenuItems.Remove(menuItem);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool MenuItemExists(int id)
        {
            return _context.MenuItems.Any(e => e.Id == id);
        }
    }
}