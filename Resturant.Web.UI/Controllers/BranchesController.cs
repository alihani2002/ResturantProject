using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class BranchesController : Controller
    {
        private readonly AppDbContext _context;

        public BranchesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var branches = await _context.Branches
                .Where(b => !b.IsDeleted)
                .OrderBy(b => b.Name)
                .ToListAsync();
            return View(branches);
        }

        public IActionResult Create()
        {
            bool isIframe = Request.Query["iframe"] == "true";
            ViewBag.IsIframe = isIframe;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Branch branch)
        {
            if (ModelState.IsValid)
            {
                branch.CreatedOn = DateTime.Now;
                branch.IsDeleted = false;

                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { iframe = Request.Query["iframe"] == "true" || (Request.HasFormContentType && Request.Form["iframe"] == "true") ? "true" : null });
            }
            ViewBag.IsIframe = Request.Query["iframe"] == "true" || (Request.HasFormContentType && Request.Form["iframe"] == "true");
            return View(branch);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null || branch.IsDeleted) return NotFound();

            ViewBag.IsIframe = Request.Query["iframe"] == "true";
            return View(branch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Branch branch)
        {
            if (id != branch.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingBranch = await _context.Branches.FindAsync(id);
                    if (existingBranch == null || existingBranch.IsDeleted) return NotFound();

                    existingBranch.Name = branch.Name;
                    existingBranch.Address = branch.Address;
                    existingBranch.ContactPhone = branch.ContactPhone;
                    existingBranch.IsActive = branch.IsActive;
                    existingBranch.LastUpdatedOn = DateTime.Now;

                    _context.Update(existingBranch);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BranchExists(branch.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index), new { iframe = Request.Query["iframe"] == "true" || (Request.HasFormContentType && Request.Form["iframe"] == "true") ? "true" : null });
            }
            ViewBag.IsIframe = Request.Query["iframe"] == "true" || (Request.HasFormContentType && Request.Form["iframe"] == "true");
            return View(branch);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null)
            {
                return Json(new { success = false, message = "Branch not found" });
            }

            // Enforce that we don't delete the last branch
            var activeCount = await _context.Branches.CountAsync(b => !b.IsDeleted);
            if (activeCount <= 1)
            {
                return Json(new { success = false, message = "Cannot delete the last remaining branch. A restaurant must have at least one branch." });
            }

            branch.IsDeleted = true;
            branch.IsActive = false;
            branch.LastUpdatedOn = DateTime.Now;

            _context.Update(branch);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Branch deleted successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            branch.IsActive = !branch.IsActive;
            branch.LastUpdatedOn = DateTime.Now;

            _context.Update(branch);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { iframe = Request.Query["iframe"] == "true" ? "true" : null });
        }

        private bool BranchExists(int id)
        {
            return _context.Branches.Any(e => e.Id == id && !e.IsDeleted);
        }
    }
}
