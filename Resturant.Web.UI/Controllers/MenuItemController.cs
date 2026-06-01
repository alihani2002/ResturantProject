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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MenuItem menuItem, Microsoft.AspNetCore.Http.IFormFile? imageFile)
        {
            if (id != menuItem.Id)
                return NotFound();

            // Load existing entity with its AddOns
            var existing = await _context.MenuItems
                .Include(m => m.AddOns)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (existing == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                // Repopulate dropdowns before returning view
                ViewBag.MenuCategories = _context.MenuCategories.ToList();
                ViewData["MenuCategoryId"] = new SelectList(_context.MenuCategories, "Id", "Name", menuItem.MenuCategoryId);
                return View(menuItem);
            }

            // Image handling
            if (imageFile != null && imageFile.Length > 0)
                menuItem.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
            else
                menuItem.ImageUrl = existing.ImageUrl; // keep old image

            // Update scalar fields of existing entity
            existing.Name = menuItem.Name;
            existing.Description = menuItem.Description;
            existing.Price = menuItem.Price;
            existing.Cost = menuItem.Cost;
            existing.ImageUrl = menuItem.ImageUrl;
            existing.IsAvailable = menuItem.IsAvailable;
            existing.IsPopular = menuItem.IsPopular;
            existing.IsTrending = menuItem.IsTrending;
            existing.IsRecommended = menuItem.IsRecommended;
            existing.OrderNumber = menuItem.OrderNumber;
            existing.MenuCategoryId = menuItem.MenuCategoryId;
            // BranchId should stay unchanged – it’s part of the multi‑branch design

            // Process AddOns
            var postedAddOns = menuItem.AddOns ?? new List<MenuItemAddOn>();

            // Update existing or add new add‑ons
            foreach (var posted in postedAddOns)
            {
                if (posted.Id == 0)
                {
                    // New add‑on
                    posted.IsDeleted = false;
                    existing.AddOns.Add(posted);
                }
                else
                {
                    var existingAddOn = existing.AddOns.FirstOrDefault(a => a.Id == posted.Id);
                    if (existingAddOn != null)
                    {
                        existingAddOn.Name = posted.Name;
                        existingAddOn.ExtraPrice = posted.ExtraPrice;
                        // Preserve IsDeleted flag (should be false for active add‑ons)
                    }
                }
            }

            // Soft‑delete any add‑on that was removed from the posted list
            var postedIds = postedAddOns.Select(a => a.Id).ToHashSet();
            foreach (var addOn in existing.AddOns.Where(a => !postedIds.Contains(a.Id)).ToList())
            {
                bool referenced = await _context.OrderItemAddOns
                    .AnyAsync(o => o.MenuItemAddOnId == addOn.Id);

                if (referenced)
                {
                    ModelState.AddModelError(string.Empty,
                        $"Add‑on \"{addOn.Name}\" cannot be removed because it is used in existing orders.");
                    // Keep it alive – do not soft‑delete
                }
                else
                {
                    addOn.IsDeleted = true;
                }
            }

            // Persist changes
            _context.Update(existing);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: MenuItem/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var menuItem = await _context.MenuItems
                .Include(m => m.AddOns)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (menuItem == null)
                return NotFound();

            // --- NEW: correct FK validation ---------------------------------
            var addOnIds = menuItem.AddOns.Select(a => a.Id).ToList();

            bool hasOrders = await _context.OrderItemAddOns
                .AnyAsync(o => addOnIds.Contains(o.MenuItemAddOnId));

            if (hasOrders)
            {
                ModelState.AddModelError(string.Empty,
                    "Cannot delete this menu item because it is referenced by existing orders. " +
                    "The item has been marked as deleted instead.");
                // Soft‑delete anyway (requirement 4)
                menuItem.IsDeleted = true;
                foreach (var addOn in menuItem.AddOns)
                    addOn.IsDeleted = true;

                _context.Update(menuItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            // ----------------------------------------------------------------

            // Soft delete the item and its add‑ons
            menuItem.IsDeleted = true;
            foreach (var addOn in menuItem.AddOns)
                addOn.IsDeleted = true;

            _context.Update(menuItem);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool MenuItemExists(int id)
        {
            return _context.MenuItems.Any(e => e.Id == id);
        }
    }
}