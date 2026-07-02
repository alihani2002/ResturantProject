/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Resturant.Core.Interfaces
{
    public interface IOrderService
    {
        Task<Order> PlaceOrderAsync(Order order);
        Task<Order> AddToExistingOrderAsync(int orderId, List<OrderItem> newItems);
        Task<IEnumerable<Order>> GetOrdersByPhoneAsync(string phoneNumber);
        Task<Order?> GetOrderWithTrackingAsync(int orderId);
        Task UpdateOrderStatusAsync(int orderId, OrderStatus status);
        Task<IEnumerable<Order>> GetPendingDeliveryOrdersAsync(int branchId);
        Task<IEnumerable<Driver>> GetAvailableDriversAsync(int branchId);
        Task AssignDriverAsync(int orderId, int driverId);
        Task SettleDriverTripAsync(int driverId, int orderId, decimal collectedCash, string cashierId, string? notes);
    }
}
