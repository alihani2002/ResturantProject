using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resturant.API.Services;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeliveryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DeliveryController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/delivery/track
        [HttpGet("track")]
        [AllowAnonymous]
        public async Task<IActionResult> TrackOrdersByPhone([FromQuery] string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest("Phone number is required for tracking.");
            }

            var orders = await _context.Orders
                .Include(o => o.DeliveryAddress)
                .Include(o => o.Driver)
                .Where(o => o.PhoneNumber == phone && o.OrderType == OrderType.Delivery)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var result = orders.Select(o =>
            {
                var loc = DriverLocationTracker.GetLocation(o.Id);
                return new
                {
                    id = o.Id,
                    status = o.Status.ToString(),
                    statusInt = (int)o.Status,
                    totalAmount = o.TotalAmount,
                    orderDate = o.OrderDate,
                    deliveryFee = o.DeliveryFee,
                    driverName = o.Driver != null ? o.Driver.FullName : null,
                    driverPhone = o.Driver != null ? o.Driver.PhoneNumber : null,
                    address = o.DeliveryAddress != null 
                        ? $"{o.DeliveryAddress.Governorate}, {o.DeliveryAddress.Area}, {o.DeliveryAddress.Street}, Bldg {o.DeliveryAddress.Building}, Floor {o.DeliveryAddress.Floor}, Apt {o.DeliveryAddress.ApartmentNumber}" 
                        : "",
                    note = o.Note,
                    driverLatitude = loc?.Latitude,
                    driverLongitude = loc?.Longitude,
                    isDriverTrackingActive = loc != null
                };
            }).ToList();

            return Ok(result);
        }

        // GET: api/delivery/track/{orderId}
        [HttpGet("track/{orderId}")]
        [AllowAnonymous]
        public async Task<IActionResult> TrackOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.DeliveryAddress)
                .Include(o => o.Driver)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.OrderType == OrderType.Delivery);

            if (order == null)
            {
                return NotFound("Order not found.");
            }

            var loc = DriverLocationTracker.GetLocation(order.Id);

            return Ok(new
            {
                id = order.Id,
                status = order.Status.ToString(),
                statusInt = (int)order.Status,
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                deliveryFee = order.DeliveryFee,
                driverName = order.Driver?.FullName,
                driverPhone = order.Driver?.PhoneNumber,
                address = order.DeliveryAddress != null 
                    ? $"{order.DeliveryAddress.Governorate}, {order.DeliveryAddress.Area}, {order.DeliveryAddress.Street}, Bldg {order.DeliveryAddress.Building}, Floor {order.DeliveryAddress.Floor}, Apt {order.DeliveryAddress.ApartmentNumber}" 
                    : "",
                note = order.Note,
                driverLatitude = loc?.Latitude,
                driverLongitude = loc?.Longitude,
                isDriverTrackingActive = loc != null
            });
        }
    }
}
