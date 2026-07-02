using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.API.Hubs
{
    public class OrderHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;
        private readonly ILogger<OrderHub> _logger;

        public OrderHub(
            UserManager<ApplicationUser> userManager,
            AppDbContext context,
            ILogger<OrderHub> _logger)
        {
            _userManager = userManager;
            _context = context;
            this._logger = _logger;
        }

        public override async Task OnConnectedAsync()
        {
            var connectionId = Context.ConnectionId;
            var user = Context.User;
            string userId = "Anonymous";
            string roleStr = "None";
            int? branchId = null;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var appUser = await _userManager.GetUserAsync(user);
                if (appUser != null)
                {
                    userId = appUser.Id;
                    branchId = appUser.BranchId;
                    var roles = await _userManager.GetRolesAsync(appUser);
                    roleStr = roles.Any() ? string.Join(", ", roles) : "None";
                }
            }

            _logger.LogInformation(
                "API SignalR Connection Established: ConnectionId={ConnectionId}, UserId={UserId}, Roles={Roles}, BranchId={BranchId}",
                connectionId, userId, roleStr, branchId ?? 0);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            var user = Context.User;
            string userId = "Anonymous";
            string roleStr = "None";
            int? branchId = null;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var appUser = await _userManager.GetUserAsync(user);
                if (appUser != null)
                {
                    userId = appUser.Id;
                    branchId = appUser.BranchId;
                    var roles = await _userManager.GetRolesAsync(appUser);
                    roleStr = roles.Any() ? string.Join(", ", roles) : "None";
                }
            }

            if (exception != null)
            {
                _logger.LogError(exception,
                    "API SignalR Connection Disconnected with Error: ConnectionId={ConnectionId}, UserId={UserId}, Roles={Roles}, BranchId={BranchId}",
                    connectionId, userId, roleStr, branchId ?? 0);
            }
            else
            {
                _logger.LogInformation(
                    "API SignalR Connection Disconnected: ConnectionId={ConnectionId}, UserId={UserId}, Roles={Roles}, BranchId={BranchId}",
                    connectionId, userId, roleStr, branchId ?? 0);
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinDashboard(string dashboardType, int? branchId)
        {
            var connectionId = Context.ConnectionId;
            var user = Context.User;
            string userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

            await Groups.AddToGroupAsync(connectionId, dashboardType);
            _logger.LogInformation(
                "API SignalR Connection Joined Group: ConnectionId={ConnectionId}, UserId={UserId}, Group={GroupName}",
                connectionId, userId, dashboardType);

            if (branchId.HasValue && branchId.Value > 0)
            {
                string branchGroup = $"{dashboardType}_{branchId.Value}";
                await Groups.AddToGroupAsync(connectionId, branchGroup);
                _logger.LogInformation(
                    "API SignalR Connection Joined Branch Group: ConnectionId={ConnectionId}, UserId={UserId}, Group={GroupName}",
                    connectionId, userId, branchGroup);
            }
        }

        public async Task LeaveDashboard(string dashboardType, int? branchId)
        {
            var connectionId = Context.ConnectionId;
            var user = Context.User;
            string userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

            await Groups.RemoveFromGroupAsync(connectionId, dashboardType);
            _logger.LogInformation(
                "API SignalR Connection Left Group: ConnectionId={ConnectionId}, UserId={UserId}, Group={GroupName}",
                connectionId, userId, dashboardType);

            if (branchId.HasValue && branchId.Value > 0)
            {
                string branchGroup = $"{dashboardType}_{branchId.Value}";
                await Groups.RemoveFromGroupAsync(connectionId, branchGroup);
                _logger.LogInformation(
                    "API SignalR Connection Left Branch Group: ConnectionId={ConnectionId}, UserId={UserId}, Group={GroupName}",
                    connectionId, userId, branchGroup);
            }
        }

        public async Task CallWaiter(int tableNumber, int branchId)
        {
            var notifyData = new { tableNumber = tableNumber };
            await Clients.Group($"waiter_{branchId}").SendAsync("CallWaiterReceived", notifyData);
            await Clients.Group($"admin_{branchId}").SendAsync("CallWaiterReceived", notifyData);
        }

        public async Task ClearWaiterCall(int tableNumber, int branchId)
        {
            var notifyData = new { tableNumber = tableNumber };
            await Clients.Group($"waiter_{branchId}").SendAsync("ClearWaiterCallReceived", notifyData);
            await Clients.Group($"admin_{branchId}").SendAsync("ClearWaiterCallReceived", notifyData);
        }

        public async Task JoinOrderTracker(int orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
            _logger.LogInformation("API SignalR Customer joined tracker for Order={OrderId}", orderId);
        }

        public async Task NotifyOrderStatusChanged(int orderId, string status)
        {
            await Clients.Group($"order_{orderId}").SendAsync("OrderStatusChanged", new { orderId = orderId, status = status });
        }

        public async Task NotifyDriverLocation(int orderId, double latitude, double longitude)
        {
            await Clients.Group($"order_{orderId}").SendAsync("DriverLocationChanged", new { orderId = orderId, latitude = latitude, longitude = longitude });
        }
    }
}
