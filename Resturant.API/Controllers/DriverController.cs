using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Resturant.API.Hubs;
using Resturant.API.Models;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Resturant.API.Controllers
{
    [Authorize(Roles = AppRoles.Driver)]
    [ApiController]
    [Route("api/[controller]")]
    public class DriverController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<OrderHub> _hubContext;

        public DriverController(
            AppDbContext context, 
            UserManager<ApplicationUser> userManager, 
            IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        // Helper to retrieve driver profile for the current logged-in user
        private async Task<Driver?> GetCurrentDriverAsync()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return null;

            return await _context.Set<Driver>()
                .FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
        }

        // GET: api/driver/active-orders
        [HttpGet("active-orders")]
        public async Task<IActionResult> GetActiveOrders()
        {
            var driver = await GetCurrentDriverAsync();
            if (driver == null)
            {
                return NotFound("Driver profile not found.");
            }

            var activeOrders = await _context.Orders
                .Include(o => o.DeliveryAddress)
                .Where(o => o.DriverId == driver.Id && 
                            (o.Status == OrderStatus.OutForDelivery || o.Status == OrderStatus.Ready))
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new DriverTripResponse
                {
                    OrderId = o.Id,
                    CustomerName = o.CustomerName ?? "",
                    PhoneNumber = o.PhoneNumber ?? "",
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status.ToString(),
                    DeliveryFee = o.DeliveryFee,
                    Address = o.DeliveryAddress != null 
                        ? $"{o.DeliveryAddress.Governorate}, {o.DeliveryAddress.Area}, {o.DeliveryAddress.Street}, Bldg {o.DeliveryAddress.Building}, Floor {o.DeliveryAddress.Floor}, Apt {o.DeliveryAddress.ApartmentNumber}" 
                        : "N/A",
                    Note = o.Note
                })
                .ToListAsync();

            return Ok(activeOrders);
        }

        // POST: api/driver/orders/{orderId}/start
        [HttpPost("orders/{orderId}/start")]
        public async Task<IActionResult> StartTrip(int orderId)
        {
            var driver = await GetCurrentDriverAsync();
            if (driver == null) return NotFound("Driver profile not found.");

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.DriverId == driver.Id);
            if (order == null) return NotFound("Order not found or not assigned to this driver.");

            if (order.Status != OrderStatus.Ready && order.Status != OrderStatus.OutForDelivery)
            {
                return BadRequest($"Order is in '{order.Status}' state and cannot be started.");
            }

            order.Status = OrderStatus.OutForDelivery;
            driver.Status = DriverStatus.OnDelivery;
            await _context.SaveChangesAsync();

            // Real-time notifications
            var orderData = new { orderId = order.Id, status = OrderStatus.OutForDelivery.ToString() };
            await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.OutForDelivery.ToString() });
            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.OutForDelivery.ToString() });
            await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.OutForDelivery.ToString() });

            return Ok(new { message = "Trip started successfully. Customer and cashier have been notified.", status = order.Status.ToString() });
        }

        // POST: api/driver/orders/{orderId}/complete
        [HttpPost("orders/{orderId}/complete")]
        public async Task<IActionResult> CompleteOrder(int orderId)
        {
            var driver = await GetCurrentDriverAsync();
            if (driver == null) return NotFound("Driver profile not found.");

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.DriverId == driver.Id);
            if (order == null) return NotFound("Order not found or not assigned to this driver.");

            if (order.Status != OrderStatus.OutForDelivery)
            {
                return BadRequest("Order is not currently out for delivery.");
            }

            order.Status = OrderStatus.Delivered;
            driver.Status = DriverStatus.Idle;
            await _context.SaveChangesAsync();

            // Clear cached location
            Resturant.API.Services.DriverLocationTracker.ClearLocation(orderId);

            // Real-time notifications
            var orderData = new { orderId = order.Id, status = OrderStatus.Delivered.ToString() };
            await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.Delivered.ToString() });
            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.Delivered.ToString() });
            await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.Delivered.ToString() });

            return Ok(new { message = "Order marked as delivered. Final settlement is required with the cashier.", status = order.Status.ToString() });
        }

        // POST: api/driver/orders/{orderId}/fail
        [HttpPost("orders/{orderId}/fail")]
        public async Task<IActionResult> FailOrder(int orderId, [FromQuery] string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest("A failure reason is required.");
            }

            var driver = await GetCurrentDriverAsync();
            if (driver == null) return NotFound("Driver profile not found.");

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.DriverId == driver.Id);
            if (order == null) return NotFound("Order not found or not assigned to this driver.");

            if (order.Status != OrderStatus.OutForDelivery)
            {
                return BadRequest("Order is not currently out for delivery.");
            }

            order.Status = OrderStatus.FailedDelivery;
            order.Note = string.IsNullOrEmpty(order.Note) 
                ? $"Failure Reason: {reason}" 
                : $"{order.Note} | Failure Reason: {reason}";
            
            driver.Status = DriverStatus.Idle;
            await _context.SaveChangesAsync();

            // Clear cached location
            Resturant.API.Services.DriverLocationTracker.ClearLocation(orderId);

            // Real-time notifications
            var orderData = new { orderId = order.Id, status = OrderStatus.FailedDelivery.ToString(), failureReason = reason };
            await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusChanged", orderData);
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.FailedDelivery.ToString() });
            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.FailedDelivery.ToString() });
            await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = OrderStatus.FailedDelivery.ToString() });

            return Ok(new { message = "Order marked as failed delivery.", status = order.Status.ToString() });
        }

        // PUT: api/driver/status
        [HttpPut("status")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateDriverStatusRequest request)
        {
            var driver = await GetCurrentDriverAsync();
            if (driver == null) return NotFound("Driver profile not found.");

            driver.Status = request.Status;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Status updated successfully.", status = driver.Status.ToString() });
        }

        // POST: api/driver/orders/{orderId}/location
        [HttpPost("orders/{orderId}/location")]
        public async Task<IActionResult> UpdateLocation(int orderId, [FromBody] DriverLocationRequest request)
        {
            var driver = await GetCurrentDriverAsync();
            if (driver == null) return NotFound("Driver profile not found.");

            var orderExists = await _context.Orders.AnyAsync(o => o.Id == orderId && o.DriverId == driver.Id);
            if (!orderExists)
            {
                return NotFound("Order not found or not assigned to this driver.");
            }

            // Save location to cache tracker
            Resturant.API.Services.DriverLocationTracker.UpdateLocation(orderId, request.Latitude, request.Longitude);

            // Broadcast driver coordinates to customer tracking group via SignalR
            await _hubContext.Clients.Group($"order_{orderId}").SendAsync("DriverLocationChanged", new
            {
                orderId = orderId,
                latitude = request.Latitude,
                longitude = request.Longitude
            });

            return Ok(new { message = "Location broadcasted successfully." });
        }

        // GET: api/driver/history
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var driver = await GetCurrentDriverAsync();
            if (driver == null)
            {
                return NotFound("Driver profile not found.");
            }

            var historyOrders = await _context.Orders
                .Include(o => o.DeliveryAddress)
                .Where(o => o.DriverId == driver.Id && 
                            (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid || o.Status == OrderStatus.FailedDelivery || o.Status == OrderStatus.Delivered))
                .OrderByDescending(o => o.OrderDate)
                .Take(50)
                .Select(o => new DriverTripResponse
                {
                    OrderId = o.Id,
                    CustomerName = o.CustomerName ?? "",
                    PhoneNumber = o.PhoneNumber ?? "",
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status.ToString(),
                    DeliveryFee = o.DeliveryFee,
                    Address = o.DeliveryAddress != null 
                        ? $"{o.DeliveryAddress.Governorate}, {o.DeliveryAddress.Area}, {o.DeliveryAddress.Street}, Bldg {o.DeliveryAddress.Building}, Floor {o.DeliveryAddress.Floor}, Apt {o.DeliveryAddress.ApartmentNumber}" 
                        : "N/A",
                    Note = o.Note
                })
                .ToListAsync();

            return Ok(historyOrders);
        }
    }
}
