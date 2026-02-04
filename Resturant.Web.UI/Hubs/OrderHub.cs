using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Resturant.Core.Entities;

namespace Resturant.Web.UI.Hubs
{
    public class OrderHub : Hub
    {
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