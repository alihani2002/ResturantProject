using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Resturant.Core.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Resturant.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Resturant.Web.UI.Hubs
{
    public class OrderHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;
        private readonly ILogger<OrderHub> _logger;

        public OrderHub(
            UserManager<ApplicationUser> userManager,
            AppDbContext context,
            ILogger<OrderHub> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
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
                "SignalR Connection Established: ConnectionId={ConnectionId}, UserId={UserId}, Roles={Roles}, BranchId={BranchId}",
                connectionId, userId, roleStr, branchId ?? 0);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception exception)
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
                    "SignalR Connection Disconnected with Error: ConnectionId={ConnectionId}, UserId={UserId}, Roles={Roles}, BranchId={BranchId}",
                    connectionId, userId, roleStr, branchId ?? 0);
            }
            else
            {
                _logger.LogInformation(
                    "SignalR Connection Disconnected: ConnectionId={ConnectionId}, UserId={UserId}, Roles={Roles}, BranchId={BranchId}",
                    connectionId, userId, roleStr, branchId ?? 0);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Join a group based on the dashboard type (waiter, chef, cashier, admin) and branch ID
        public async Task JoinDashboard(string dashboardType, int? branchId)
        {
            var connectionId = Context.ConnectionId;
            var user = Context.User;
            string userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

            await Groups.AddToGroupAsync(connectionId, dashboardType);
            _logger.LogInformation(
                "SignalR Connection Joined Group: ConnectionId={ConnectionId}, UserId={UserId}, Group={GroupName}",
                connectionId, userId, dashboardType);

            if (branchId.HasValue && branchId.Value > 0)
            {
                string branchGroup = $"{dashboardType}_{branchId.Value}";
                await Groups.AddToGroupAsync(connectionId, branchGroup);
                _logger.LogInformation(
                    "SignalR Connection Joined Branch Group: ConnectionId={ConnectionId}, UserId={UserId}, Group={GroupName}",
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
                "SignalR Connection Left Group: ConnectionId={ConnectionId}, UserId={UserId}, Group={GroupName}",
                connectionId, userId, dashboardType);

            if (branchId.HasValue && branchId.Value > 0)
            {
                string branchGroup = $"{dashboardType}_{branchId.Value}";
                await Groups.RemoveFromGroupAsync(connectionId, branchGroup);
                _logger.LogInformation(
                    "SignalR Connection Left Branch Group: ConnectionId={ConnectionId}, UserId={UserId}, Group={GroupName}",
                    connectionId, userId, branchGroup);
            }
        }

        public async Task CallWaiter(int tableNumber, int branchId)
        {
            var notifyData = new { tableNumber = tableNumber };
            await Clients.Group($"waiter_{branchId}").SendAsync("CallWaiterReceived", notifyData);
            await Clients.Group($"admin_{branchId}").SendAsync("CallWaiterReceived", notifyData);
            _logger.LogInformation("CallWaiter invoked for Table={TableNumber}, Branch={BranchId}", tableNumber, branchId);
        }

        public async Task ClearWaiterCall(int tableNumber, int branchId)
        {
            var notifyData = new { tableNumber = tableNumber };
            await Clients.Group($"waiter_{branchId}").SendAsync("ClearWaiterCallReceived", notifyData);
            await Clients.Group($"admin_{branchId}").SendAsync("ClearWaiterCallReceived", notifyData);
            _logger.LogInformation("ClearWaiterCall invoked for Table={TableNumber}, Branch={BranchId}", tableNumber, branchId);
        }

        // New order created - notify waiters
        public async Task NotifyNewOrder(object orderData)
        {
            await Clients.Group("waiter").SendAsync("NewOrderReceived", orderData);
            await Clients.Group("admin").SendAsync("NewOrderReceived", orderData);
            await Clients.All.SendAsync("OrderStatusChanged", orderData);
        }

        // Order accepted by waiter - notify chef and cashier
        public async Task NotifyOrderAccepted(object orderData)
        {
            await Clients.Group("chef").SendAsync("OrderAccepted", orderData);
            await Clients.Group("cashier").SendAsync("OrderAccepted", orderData);
            await Clients.Group("waiter").SendAsync("OrderStatusChanged", orderData);
            await Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
            await Clients.All.SendAsync("OrderStatusChanged", orderData);
        }

        // Order ready by chef - notify waiter and cashier
        public async Task NotifyOrderReady(object orderData)
        {
            await Clients.Group("waiter").SendAsync("OrderReady", orderData);
            await Clients.Group("cashier").SendAsync("OrderReady", orderData);
            await Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
            await Clients.All.SendAsync("OrderStatusChanged", orderData);
        }

        // Payment processed - notify waiter
        public async Task NotifyPaymentProcessed(object orderData)
        {
            await Clients.Group("waiter").SendAsync("PaymentProcessed", orderData);
            await Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
            await Clients.All.SendAsync("OrderStatusChanged", orderData);
        }

        // Table cleared
        public async Task NotifyTableCleared(object orderData)
        {
            await Clients.Group("waiter").SendAsync("TableCleared", orderData);
            await Clients.Group("cashier").SendAsync("OrderCompleted", orderData);
            await Clients.Group("admin").SendAsync("OrderStatusChanged", orderData);
            await Clients.All.SendAsync("OrderStatusChanged", orderData);
        }

        // Legacy methods for backward compatibility
        public async Task SendOrderUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveOrderUpdate", message);
        }

        public async Task SendWaiterUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveWaiterUpdate", message);
        }

        public async Task SendChefUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveChefUpdate", message);
        }

        public async Task SendCashierUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveCashierUpdate", message);
        }

        public async Task SendOrderDetails(Order order)
        {
            await Clients.All.SendAsync("ReceiveOrderDetails", order);
        }

        public async Task JoinOrderTracker(int orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
            _logger.LogInformation("SignalR Customer joined tracker for Order={OrderId}", orderId);
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