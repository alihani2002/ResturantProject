using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resturant.Core.Common;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Chief + "," + AppRoles.Admin + "," + AppRoles.Manager)]
    public class ChefController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}