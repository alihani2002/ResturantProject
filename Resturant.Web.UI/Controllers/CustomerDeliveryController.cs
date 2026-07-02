using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using Resturant.Infrastructure.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    public class CustomerDeliveryController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IOrderService _orderService;

        public CustomerDeliveryController(AppDbContext context, IOrderService orderService)
        {
            _context = context;
            _orderService = orderService;
        }

        // GET: /CustomerDelivery
        public async Task<IActionResult> Index(int? branchId)
        {
            var branches = await _context.Branches
                .Where(b => b.IsActive && !b.IsDeleted)
                .ToListAsync();

            if (!branches.Any())
            {
                return Content("No active branches available at the moment.");
            }

            int selectedBranchId = branchId ?? branches.First().Id;
            ViewBag.SelectedBranchId = selectedBranchId;
            ViewBag.Branches = branches;

            // Load zones for the checkout form dropdown
            var zones = await _context.DeliveryZones
                .Where(z => z.IsActive && !z.IsDeleted)
                .ToListAsync();
            ViewBag.DeliveryZones = zones;

            // Load categories and items for the selected branch
            var categories = await _context.MenuCategories
                .Include(c => c.MenuItems)
                .ThenInclude(m => m.Sizes)
                .Include(c => c.MenuItems)
                .ThenInclude(m => m.AddOns)
                .Where(c => c.IsActive && c.BranchId == selectedBranchId && c.MenuItems.Any(mi => mi.IsAvailable))
                .OrderBy(c => c.OrderNumber)
                .ToListAsync();

            return View(categories);
        }

        // GET: /CustomerDelivery/Track
        public IActionResult Track()
        {
            return View();
        }
    }
}
