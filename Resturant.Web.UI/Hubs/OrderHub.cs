using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Resturant.Core.Entities;
using System.Security.Claims;

namespace Resturant.Web.UI.Hubs
{
    public class OrderHub : Hub
    {
        // Join a group based on the dashboard type (waiter, chef, cashier, admin)
        public async Task JoinDashboard(string dashboardType)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, dashboardType);
        }

        public async Task LeaveDashboard(string dashboardType)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, dashboardType);
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
    }
}