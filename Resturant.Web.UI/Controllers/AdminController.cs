using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    // Allow any role except specific 'User' role, but practically we whitelist allowed roles
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Chief + "," + AppRoles.Waiter + "," + AppRoles.Manager + "," + AppRoles.Accountant)]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
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
    }
}
