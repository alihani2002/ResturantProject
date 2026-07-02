using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using Resturant.Web.UI.ViewModels;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly AppDbContext _context;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IPasswordHasher<ApplicationUser> passwordHasher, AppDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _passwordHasher = passwordHasher;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.Include(u => u.Branch).ToListAsync();
            var userViewModels = new List<UserViewModel>();

            foreach (var user in users)
            {
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
                userViewModels.Add(new UserViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName ?? "N/A",
                    Email = user.Email,
                    Role = role ?? "None",
                    IsActive = user.IsActive,
                    IsDeleted = user.IsDeleted,
                    BranchId = user.BranchId,
                    BranchName = user.Branch?.Name ?? "All Branches / Admin"
                });
            }

            return View(userViewModels);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Branches = await _context.Branches.Where(b => !b.IsDeleted && b.IsActive).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel model)
        {
            if (model.Role != AppRoles.Admin && !model.BranchId.HasValue)
            {
                ModelState.AddModelError("BranchId", "A branch must be selected for this role.");
            }

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    IsActive = model.IsActive,
                    IsDeleted = false,
                    CreatedOn = DateTime.Now,
                    EmailConfirmed = true,
                    BranchId = model.Role == AppRoles.Admin ? null : model.BranchId
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(model.Role))
                    {
                        if (!await _roleManager.RoleExistsAsync(model.Role))
                        {
                            await _roleManager.CreateAsync(new IdentityRole(model.Role));
                        }
                        await _userManager.AddToRoleAsync(user, model.Role);

                        // If role is Driver, also create Driver record
                        if (model.Role == AppRoles.Driver)
                        {
                            var driver = new Driver
                            {
                                UserId = user.Id,
                                FullName = user.FullName,
                                PhoneNumber = user.PhoneNumber ?? user.Email ?? "",
                                Status = DriverStatus.Idle,
                                BranchId = model.BranchId ?? 1,
                                CreatedOn = DateTime.Now
                            };
                            _context.Set<Driver>().Add(driver);
                            await _context.SaveChangesAsync();
                        }
                    }
                    return RedirectToAction(nameof(Index), new { iframe = Request.Query["iframe"] == "true" || (Request.HasFormContentType && Request.Form["iframe"] == "true") ? "true" : null });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ViewBag.Branches = await _context.Branches.Where(b => !b.IsDeleted && b.IsActive).ToListAsync();
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var model = new UserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                IsActive = user.IsActive,
                IsDeleted = user.IsDeleted,
                Role = roles.FirstOrDefault(),
                BranchId = user.BranchId
            };

            ViewBag.Branches = await _context.Branches.Where(b => !b.IsDeleted && b.IsActive).ToListAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserViewModel model)
        {
            if (model.Id == null) return NotFound();
            
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            if (model.Role != AppRoles.Admin && !model.BranchId.HasValue)
            {
                ModelState.AddModelError("BranchId", "A branch must be selected for this role.");
            }

            if (ModelState.IsValid)
            {
                user.FullName = model.FullName;
                user.Email = model.Email;
                user.UserName = model.Email;
                user.IsActive = model.IsActive;
                user.IsDeleted = model.IsDeleted;
                user.BranchId = model.Role == AppRoles.Admin ? null : model.BranchId;

                if (!string.IsNullOrEmpty(model.Password))
                {
                    user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
                }

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    if (!currentRoles.Contains(model.Role))
                    {
                        await _userManager.RemoveFromRolesAsync(user, currentRoles);
                        if (!await _roleManager.RoleExistsAsync(model.Role))
                        {
                            await _roleManager.CreateAsync(new IdentityRole(model.Role));
                        }
                        await _userManager.AddToRoleAsync(user, model.Role);
                    }

                    // Sync Driver entity status
                    var existingDriver = await _context.Set<Driver>().FirstOrDefaultAsync(d => d.UserId == user.Id);
                    if (model.Role == AppRoles.Driver)
                    {
                        if (existingDriver == null)
                        {
                            var driver = new Driver
                            {
                                UserId = user.Id,
                                FullName = user.FullName,
                                PhoneNumber = user.PhoneNumber ?? user.Email ?? "",
                                Status = DriverStatus.Idle,
                                BranchId = model.BranchId ?? 1,
                                CreatedOn = DateTime.Now
                            };
                            _context.Set<Driver>().Add(driver);
                        }
                        else
                        {
                            existingDriver.FullName = user.FullName;
                            existingDriver.PhoneNumber = user.PhoneNumber ?? user.Email ?? "";
                            existingDriver.BranchId = model.BranchId ?? 1;
                            existingDriver.IsDeleted = false;
                            _context.Set<Driver>().Update(existingDriver);
                        }
                        await _context.SaveChangesAsync();
                    }
                    else if (existingDriver != null)
                    {
                        existingDriver.IsDeleted = true;
                        existingDriver.Status = DriverStatus.Offline;
                        _context.Set<Driver>().Update(existingDriver);
                        await _context.SaveChangesAsync();
                    }

                    return RedirectToAction(nameof(Index), new { iframe = Request.Query["iframe"] == "true" || (Request.HasFormContentType && Request.Form["iframe"] == "true") ? "true" : null });
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ViewBag.Branches = await _context.Branches.Where(b => !b.IsDeleted && b.IsActive).ToListAsync();
            return View(model);
        }

        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsDeleted = true;
            user.IsActive = false; // Optionally deactivate on delete
            await _userManager.UpdateAsync(user);

            return RedirectToAction(nameof(Index), new { iframe = Request.Query["iframe"] == "true" ? "true" : null });
        }

        public async Task<IActionResult> ToggleActive(string id)
        {
            if (id == null) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            
            return RedirectToAction(nameof(Index), new { iframe = Request.Query["iframe"] == "true" ? "true" : null });
        }
    }
}
