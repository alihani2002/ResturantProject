using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Resturant.Core.Common;
using Resturant.Web.UI.Models;

namespace Resturant.Web.UI.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                if (User.IsInRole(AppRoles.Admin) || 
                    User.IsInRole(AppRoles.Chief) || 
                    User.IsInRole(AppRoles.Manager) || 
                    User.IsInRole(AppRoles.Accountant) || 
                    User.IsInRole(AppRoles.Waiter))
                {
                    return RedirectToAction("Index", "Admin");
                }
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
