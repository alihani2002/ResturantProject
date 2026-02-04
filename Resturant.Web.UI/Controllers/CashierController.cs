using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resturant.Core.Common;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Accountant + "," + AppRoles.Admin + "," + AppRoles.Manager)]
    public class CashierController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}