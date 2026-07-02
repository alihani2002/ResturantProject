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

        public async Task<IEnumerable<Order>> GetPendingDeliveryOrdersAsync(int branchId)
        {
            return await _unitOfWork.Repository<Order>()
                .ListAsync(o => o.BranchId == branchId && o.OrderType == OrderType.Delivery && o.Status == OrderStatus.Pending);
        }

        public async Task<IEnumerable<Driver>> GetAvailableDriversAsync(int branchId)
        {
            return await _unitOfWork.Repository<Driver>()
                .ListAsync(d => d.BranchId == branchId && d.Status == DriverStatus.Idle);
        }

        public async Task AssignDriverAsync(int orderId, int driverId)
        {
            var order = await _unitOfWork.Repository<Order>().GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            var driver = await _unitOfWork.Repository<Driver>().GetByIdAsync(driverId);
            if (driver == null) throw new Exception("Driver not found");

            order.DriverId = driverId;
            order.Status = OrderStatus.OutForDelivery;
            
            driver.Status = DriverStatus.OnDelivery;

            _unitOfWork.Repository<Order>().Update(order);
            _unitOfWork.Repository<Driver>().Update(driver);
            await _unitOfWork.CompleteAsync();
        }

        public async Task SettleDriverTripAsync(int driverId, int orderId, decimal collectedCash, string cashierId, string? notes)
        {
            var order = await _unitOfWork.Repository<Order>().GetByIdAsync(orderId);
            if (order == null) throw new Exception("Order not found");

            var driver = await _unitOfWork.Repository<Driver>().GetByIdAsync(driverId);
            if (driver == null) throw new Exception("Driver not found");

            if (order.Status != OrderStatus.Delivered && order.Status != OrderStatus.FailedDelivery)
            {
                throw new Exception("لا يمكن إجراء تسوية لطلب لم يقم الطيار بتسليمه بعد أو تسجيل فشله.");
            }

            order.Status = OrderStatus.Completed;
            order.PaidAmount = collectedCash;
            order.ChangeReturned = collectedCash - (order.TotalAmount + order.DeliveryFee);

            driver.Status = DriverStatus.Idle;

            var settlement = new DriverSettlement
            {
                DriverId = driverId,
                OrderId = orderId,
                ExpectedCash = order.TotalAmount + order.DeliveryFee,
                CollectedCash = collectedCash,
                Status = Math.Abs(collectedCash - (order.TotalAmount + order.DeliveryFee)) < 0.01m 
                    ? SettlementStatus.Settled 
                    : SettlementStatus.Discrepancy,
                SettledById = cashierId,
                SettledAt = DateTime.Now,
                Notes = notes
            };

            _unitOfWork.Repository<Order>().Update(order);
            _unitOfWork.Repository<Driver>().Update(driver);
            await _unitOfWork.Repository<DriverSettlement>().AddAsync(settlement);
            await _unitOfWork.CompleteAsync();
        }
    }
}
