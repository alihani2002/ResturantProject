using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resturant.Core.Common;

namespace Resturant.Web.UI.Controllers
{
    // Allow any role except specific 'User' role, but practically we whitelist allowed roles
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Chief + "," + AppRoles.Waiter + "," + AppRoles.Manager + "," + AppRoles.Accountant)]
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
