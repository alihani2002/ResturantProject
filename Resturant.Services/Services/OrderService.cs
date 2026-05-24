/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Services.Services
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Order> PlaceOrderAsync(Order order)
        {
            // Logic to check for active session and merge if necessary
            var activeSession = await _unitOfWork.Repository<TableSession>()
                .GetFirstOrDefaultAsync(s => s.TableId == order.TableNumber && s.IsActive);

            if (activeSession != null)
            {
                order.TableSessionId = activeSession.Id;
                // If an order already exists in this session, we might want to merge it 
                // OR just add a new order linked to the same session.
                // The requirement says "New items must be merged into the same active order".
                var existingOrder = activeSession.Orders.FirstOrDefault(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.InPreparation);
                
                if (existingOrder != null)
                {
                    return await AddToExistingOrderAsync(existingOrder.Id, order.OrderItems.ToList());
                }
            }

            await _unitOfWork.Repository<Order>().AddAsync(order);
            await _unitOfWork.CompleteAsync();
            return order;
        }

        public async Task<Order> AddToExistingOrderAsync(int orderId, List<OrderItem> newItems)
        {
            var existingOrder = await _unitOfWork.Repository<Order>().GetByIdAsync(orderId);
            if (existingOrder == null) throw new Exception("Order not found");

            foreach (var item in newItems)
            {
                // Check if item already exists in order to increment quantity, or add new
                var existingItem = existingOrder.OrderItems.FirstOrDefault(i => i.MenuItemId == item.MenuItemId);
                if (existingItem != null)
                {
                    existingItem.Quantity += item.Quantity;
                }
                else
                {
                    existingOrder.OrderItems.Add(item);
                }
            }

            existingOrder.TotalAmount = existingOrder.OrderItems.Sum(i => i.Total);
            _unitOfWork.Repository<Order>().Update(existingOrder);
            await _unitOfWork.CompleteAsync();
            return existingOrder;
        }

        public async Task<IEnumerable<Order>> GetOrdersByPhoneAsync(string phoneNumber)
        {
            return await _unitOfWork.Repository<Order>().ListAsync(o => o.PhoneNumber == phoneNumber);
        }

        public async Task<Order?> GetOrderWithTrackingAsync(int orderId)
        {
            return await _unitOfWork.Repository<Order>().GetByIdAsync(orderId);
        }

        public async Task UpdateOrderStatusAsync(int orderId, OrderStatus status)
        {
            var order = await _unitOfWork.Repository<Order>().GetByIdAsync(orderId);
            if (order != null)
            {
                order.Status = status;
                _unitOfWork.Repository<Order>().Update(order);
                await _unitOfWork.CompleteAsync();
            }
        }
    }
}
