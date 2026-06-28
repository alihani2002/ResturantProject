using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using Resturant.Web.UI.Hubs;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Chief + "," + AppRoles.Admin + "," + AppRoles.Manager)]
    public class ChefController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChefController(AppDbContext context, IHubContext<OrderHub> hubContext, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _hubContext = hubContext;
            _userManager = userManager;
        }

        private async Task<int> GetSelectedBranchIdAsync()
        {
            var userId = _userManager.GetUserId(User);
            var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            
            var isAdminOrManager = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);
            if (isAdminOrManager)
            {
                // Try query string
                if (Request.Query.TryGetValue("branchId", out var qBranchIdStr) && int.TryParse(qBranchIdStr, out int qBranchId))
                {
                    Response.Cookies.Append("ChefActiveBranchId", qBranchId.ToString(), new CookieOptions { HttpOnly = true, Expires = DateTime.Now.AddDays(7) });
                    return qBranchId;
                }
                
                // Try cookie
                string cookieBranchIdStr = Request.Cookies["ChefActiveBranchId"];
                if (!string.IsNullOrEmpty(cookieBranchIdStr) && int.TryParse(cookieBranchIdStr, out int cookieBranchId))
                {
                    return cookieBranchId;
                }
            }
            
            return dbUser?.BranchId ?? 1;
        }

        public async Task<IActionResult> SelectBranch()
        {
            Response.Cookies.Delete("ChefActiveBranchId");
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index(int? branchId = null)
        {
            var isAdminOrManager = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);
            
            if (isAdminOrManager)
            {
                if (branchId.HasValue)
                {
                    Response.Cookies.Append("ChefActiveBranchId", branchId.Value.ToString(), new CookieOptions { HttpOnly = true, Expires = DateTime.Now.AddDays(7) });
                }
                else
                {
                    Response.Cookies.Delete("ChefActiveBranchId");
                    
                    // Show branch selector
                    ViewBag.TerminalName = "Chef Terminal";
                    ViewBag.TargetAction = "Index";
                    ViewBag.TargetController = "Chef";
                    var branches = await _context.Branches.Where(b => !b.IsDeleted && b.IsActive).ToListAsync();
                    return View("_BranchSelector", branches);
                }
            }

            int selectedBranchId = await GetSelectedBranchIdAsync();
            ViewBag.ActiveBranchId = selectedBranchId;

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.AddOns)
                        .ThenInclude(a => a.AddOn)
                .Where(o => (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.InPreparation) && o.BranchId == selectedBranchId)
                .OrderBy(o => o.ConfirmedDate)
                .ToListAsync();

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrdersData()
        {
            int branchId = await GetSelectedBranchIdAsync();

            var ordersList = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.AddOns)
                        .ThenInclude(a => a.AddOn)
                .Where(o => (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.InPreparation) && o.BranchId == branchId)
                .OrderBy(o => o.ConfirmedDate)
                .ToListAsync();

            var orders = ordersList.Select(o => new
            {
                id = o.Id,
                tableNumber = o.TableNumber,
                status = (int)o.Status,
                note = o.Note,
                totalAmount = o.OrderItems.Where(oi => !oi.IsCancelled).Sum(oi => oi.EffectiveTotal),
                orderDate = o.OrderDate,
                orderItems = o.OrderItems.Select(oi => new
                {
                    id = oi.Id,
                    menuItemName = oi.MenuItem != null ? oi.MenuItem.GetFormattedNameWithSize(oi.SizeName, oi.Price) : "",
                    quantity = oi.Quantity,
                    price = oi.Price,
                    isCancelled = oi.IsCancelled,
                    addOns = oi.AddOns.Select(a => new
                    {
                        id = a.Id,
                        name = a.AddOn != null ? a.AddOn.Name : ""
                    }).ToList()
                })
            }).ToList();

            return Json(orders);
        }

        // Chef clicks "Start Preparing" → sets InPreparation
        [HttpPost]
        public async Task<IActionResult> StartOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();
            if (order.Status != OrderStatus.Confirmed) return BadRequest();

            order.Status = OrderStatus.InPreparation;
            await _context.SaveChangesAsync();

            var orderData = BuildOrderData(order);

            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"guest_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);

            return Ok();
        }

        // Chef clicks "Mark Ready" → sets Ready, removes from chef board
        [HttpPost]
        public async Task<IActionResult> FinishOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();
            if (order.Status == OrderStatus.Ready || order.Status == OrderStatus.Served || order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            {
                return BadRequest("Order is already ready or completed.");
            }

            order.Status = OrderStatus.Ready;
            await _context.SaveChangesAsync();

            var orderData = BuildOrderData(order);

            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("OrderReady", orderData);
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderReady", orderData);
            await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"guest_{order.BranchId}").SendAsync("OrderStatusChanged", orderData);

            await _hubContext.Clients.Group($"accountant_{order.BranchId}").SendAsync("ReceiveAccountantUpdate", "New Order Ready for Payment");
            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("ReceiveWaiterUpdate", "Order Ready");

            return Ok();
        }

        private object BuildOrderData(Order order)
        {
            return new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                branchId = order.BranchId,
                status = (int)order.Status,
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                note = order.Note,
                orderItems = order.OrderItems.Select(oi => new
                {
                    id = oi.Id,
                    menuItemName = oi.MenuItem?.GetFormattedNameWithSize(oi.SizeName, oi.Price) ?? "",
                    quantity = oi.Quantity,
                    price = oi.Price,
                    isCancelled = oi.IsCancelled
                })
            };
        }
    }
}
