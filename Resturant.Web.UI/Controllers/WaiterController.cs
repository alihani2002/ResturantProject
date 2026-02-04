using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resturant.Core.Common;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Waiter + "," + AppRoles.Admin + "," + AppRoles.Manager)]
    public class WaiterController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}