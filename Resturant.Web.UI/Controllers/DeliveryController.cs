using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using Resturant.Infrastructure.Data;
using Resturant.Web.UI.Hubs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Resturant.Web.UI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeliveryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IOrderService _orderService;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly ILogger<DeliveryController> _logger;

        public DeliveryController(
            AppDbContext context,
            IOrderService orderService,
            IHubContext<OrderHub> hubContext,
            ILogger<DeliveryController> logger)
        {
            _context = context;
            _orderService = orderService;
            _hubContext = hubContext;
            _logger = logger;
        }

        // ================= CUSTOMER FLOWS =================

        // GET: api/delivery/zones
        [HttpGet("zones")]
        public async Task<IActionResult> GetDeliveryZones()
        {
            var zones = await _context.DeliveryZones
                .Where(z => z.IsActive && !z.IsDeleted)
                .ToListAsync();
            return Ok(zones);
        }

        // POST: api/delivery/checkout
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] DeliveryCheckoutRequest request)
        {
            if (request == null || request.Items == null || !request.Items.Any() || request.Address == null)
            {
                return BadRequest("Invalid delivery checkout request data");
            }

            var zone = await _context.DeliveryZones
                .FirstOrDefaultAsync(z => z.Id == request.Address.DeliveryZoneId && z.IsActive && !z.IsDeleted);
            if (zone == null)
            {
                return BadRequest("Selected delivery zone is invalid or inactive");
            }

            // Create address entity
            var address = new DeliveryAddress
            {
                CustomerName = request.CustomerName,
                PhoneNumber = request.PhoneNumber,
                Governorate = request.Address.Governorate,
                Area = request.Address.Area,
                Street = request.Address.Street,
                Building = request.Address.Building,
                Floor = request.Address.Floor,
                ApartmentNumber = request.Address.ApartmentNumber,
                Latitude = request.Address.Latitude,
                Longitude = request.Address.Longitude,
                DeliveryInstructions = request.Address.DeliveryInstructions,
                CreatedOn = DateTime.Now
            };

            _context.DeliveryAddresses.Add(address);
            await _context.SaveChangesAsync();

            // Create Order
            var order = new Order
            {
                BranchId = request.BranchId,
                CustomerName = request.CustomerName,
                PhoneNumber = request.PhoneNumber,
                Note = request.Note,
                OrderType = OrderType.Delivery,
                Status = OrderStatus.Pending, // Pending Confirmation
                OrderDate = DateTime.Now,
                DeliveryAddressId = address.Id,
                DeliveryZoneId = zone.Id,
                DeliveryFee = zone.DeliveryFee,
                PriceCategory = "Retail",
                CreatedOn = DateTime.Now
            };

            decimal totalItemsAmount = 0;
            foreach (var itemRequest in request.Items)
            {
                var menuItem = await _context.MenuItems
                    .Include(m => m.Sizes)
                    .Include(m => m.Prices)
                    .FirstOrDefaultAsync(m => m.Id == itemRequest.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                {
                    continue;
                }

                var resolvedPrice = ResolvePrice(menuItem, order.BranchId, order.PriceCategory, itemRequest.Quantity, order.PhoneNumber);
                var selectedSize = menuItem.GetParsedSizes().FirstOrDefault(s =>
                    (!string.IsNullOrWhiteSpace(itemRequest.Size) && s.Name == itemRequest.Size) ||
                    (string.IsNullOrWhiteSpace(itemRequest.Size) && s.Price == resolvedPrice));

                var orderItem = new OrderItem
                {
                    MenuItemId = itemRequest.MenuItemId,
                    Quantity = itemRequest.Quantity,
                    Price = resolvedPrice,
                    MenuItemSizeId = selectedSize?.Id,
                    SizeName = selectedSize?.Name ?? itemRequest.Size,
                    UnitCost = selectedSize?.Cost ?? menuItem.Cost
                };

                order.OrderItems.Add(orderItem);
                _context.OrderItems.Add(orderItem);

                // Add AddOns if selected
                decimal addOnsTotal = 0;
                if (itemRequest.AddOnIds != null && itemRequest.AddOnIds.Any())
                {
                    var dbAddOns = await _context.MenuItemAddOns
                        .Where(a => itemRequest.AddOnIds.Contains(a.Id) && a.MenuItemId == itemRequest.MenuItemId)
                        .ToListAsync();

                    foreach (var addon in dbAddOns)
                    {
                        var orderItemAddOn = new OrderItemAddOn
                        {
                            OrderItem = orderItem,
                            MenuItemAddOnId = addon.Id,
                            Price = addon.ExtraPrice
                        };
                        _context.OrderItemAddOns.Add(orderItemAddOn);
                        orderItem.AddOns.Add(orderItemAddOn);
                        addOnsTotal += addon.ExtraPrice;
                    }
                }

                totalItemsAmount += (orderItem.Price + addOnsTotal) * orderItem.Quantity;
            }

            var settings = await _context.RestaurantSettings.FirstOrDefaultAsync(s => s.BranchId == order.BranchId) 
                           ?? new RestaurantSetting { TaxPercentage = 14, ServicePercentage = 12 };

            decimal serviceAmount = totalItemsAmount * (settings.ServicePercentage / 100);
            decimal taxAmount = (totalItemsAmount + serviceAmount) * (settings.TaxPercentage / 100);
            decimal grandTotal = totalItemsAmount + serviceAmount + taxAmount + order.DeliveryFee;

            order.TaxPercentage = settings.TaxPercentage;
            order.ServicePercentage = settings.ServicePercentage;
            order.ServiceAmount = serviceAmount;
            order.TaxAmount = taxAmount;
            order.TotalAmount = grandTotal;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Notify Cashier and Waiter about the new pending confirmation order
            var orderSignalRData = new
            {
                id = order.Id,
                customerName = order.CustomerName,
                phoneNumber = order.PhoneNumber,
                status = order.Status.ToString(),
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                deliveryFee = order.DeliveryFee,
                address = new
                {
                    governorate = address.Governorate,
                    area = address.Area,
                    street = address.Street,
                    building = address.Building,
                    floor = address.Floor,
                    apartmentNumber = address.ApartmentNumber
                }
            };

            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OnNewDeliveryOrder", orderSignalRData);
            await _hubContext.Clients.Group($"waiter_{order.BranchId}").SendAsync("OnNewDeliveryOrder", orderSignalRData);
            await _hubContext.Clients.Group($"admin_{order.BranchId}").SendAsync("OnNewDeliveryOrder", orderSignalRData);

            return Ok(new { orderId = order.Id, totalAmount = order.TotalAmount });
        }

        // GET: api/delivery/track
        [HttpGet("track")]
        public async Task<IActionResult> TrackOrdersByPhone([FromQuery] string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest("Phone number is required for tracking");
            }

            var orders = await _context.Orders
                .Include(o => o.DeliveryAddress)
                .Where(o => o.PhoneNumber == phone && o.OrderType == OrderType.Delivery)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    id = o.Id,
                    status = o.Status.ToString(),
                    statusInt = (int)o.Status,
                    totalAmount = o.TotalAmount,
                    orderDate = o.OrderDate,
                    deliveryFee = o.DeliveryFee,
                    driverName = o.Driver != null ? o.Driver.FullName : null,
                    driverPhone = o.Driver != null ? o.Driver.PhoneNumber : null,
                    address = o.DeliveryAddress != null ? $"{o.DeliveryAddress.Governorate}, {o.DeliveryAddress.Area}, {o.DeliveryAddress.Street}, عمارة {o.DeliveryAddress.Building}, دور {o.DeliveryAddress.Floor}, شقة {o.DeliveryAddress.ApartmentNumber}" : "",
                    note = o.Note
                })
                .ToListAsync();

            return Ok(orders);
        }

        // GET: api/delivery/track/{orderId}
        [HttpGet("track/{orderId}")]
        public async Task<IActionResult> TrackOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.DeliveryAddress)
                .Include(o => o.Driver)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.OrderType == OrderType.Delivery);

            if (order == null)
            {
                return NotFound("Order not found");
            }

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
                address = order.DeliveryAddress
            });
        }


        // ================= CASHIER FLOWS =================

        // GET: api/delivery/pending
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingDeliveryOrders([FromQuery] int branchId)
        {
            var orders = await _orderService.GetPendingDeliveryOrdersAsync(branchId);
            return Ok(orders);
        }

        // GET: api/delivery/active
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveDeliveryOrders([FromQuery] int branchId)
        {
            var orders = await _context.Orders
                .Include(o => o.DeliveryAddress)
                .Include(o => o.Driver)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.BranchId == branchId && o.OrderType == OrderType.Delivery && 
                            (o.Status == OrderStatus.Pending || 
                             o.Status == OrderStatus.Confirmed || 
                             o.Status == OrderStatus.InPreparation || 
                             o.Status == OrderStatus.Ready || 
                             o.Status == OrderStatus.OutForDelivery ||
                             o.Status == OrderStatus.Delivered ||
                             o.Status == OrderStatus.FailedDelivery))
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    id = o.Id,
                    customerName = o.CustomerName,
                    phoneNumber = o.PhoneNumber,
                    status = o.Status.ToString(),
                    totalAmount = o.TotalAmount,
                    orderDate = o.OrderDate,
                    deliveryFee = o.DeliveryFee,
                    driverId = o.DriverId,
                    driverName = o.Driver != null ? o.Driver.FullName : null,
                    driverPhone = o.Driver != null ? o.Driver.PhoneNumber : null,
                    address = o.DeliveryAddress != null ? $"{o.DeliveryAddress.Governorate}, {o.DeliveryAddress.Area}, {o.DeliveryAddress.Street}, عمارة {o.DeliveryAddress.Building}, دور {o.DeliveryAddress.Floor}, شقة {o.DeliveryAddress.ApartmentNumber}" : "",
                    note = o.Note,
                    items = o.OrderItems.Select(oi => new
                    {
                        name = oi.MenuItem != null ? oi.MenuItem.Name : "صنف غير معروف",
                        quantity = oi.Quantity,
                        price = oi.Price,
                        size = oi.SizeName
                    })
                })
                .ToListAsync();

            return Ok(orders);
        }


        // PUT: api/delivery/{id}/confirm
        [HttpPut("{id}/confirm")]
        public async Task<IActionResult> ConfirmOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound("Order not found");

            if (order.Status != OrderStatus.Pending)
            {
                return BadRequest("Order is already verified or in progress");
            }

            order.Status = OrderStatus.InPreparation;
            order.ConfirmedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            // SignalR Update to Customer
            await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusChanged", new { orderId = order.Id, status = OrderStatus.InPreparation.ToString() });

            // Send to Kitchen
            var orderData = new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                branchId = order.BranchId,
                status = order.Status.ToString(),
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                note = order.Note,
                items = order.OrderItems.Select(oi => new
                {
                    menuItemName = _context.MenuItems.Find(oi.MenuItemId)?.Name ?? "Unknown",
                    quantity = oi.Quantity,
                    price = oi.Price,
                    total = oi.Total
                }).ToList()
            };
            await _hubContext.Clients.Group($"chef_{order.BranchId}").SendAsync("OrderAccepted", orderData);
            await _hubContext.Clients.Group($"chef_{order.BranchId}").SendAsync("ReceiveChefUpdate", "New delivery order ready to prepare");

            return Ok(new { message = "Order confirmed and sent to kitchen" });
        }

        // PUT: api/delivery/{id}/reject
        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectOrder(int id, [FromBody] RejectOrderRequest request)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound("Order not found");

            order.Status = OrderStatus.Cancelled;
            order.CancelledDate = DateTime.Now;
            order.Note = string.IsNullOrWhiteSpace(request.Reason) ? order.Note : $"{order.Note} | Rejection Reason: {request.Reason}";
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusChanged", new { orderId = order.Id, status = OrderStatus.Cancelled.ToString(), reason = request.Reason });

            return Ok(new { message = "Order rejected successfully" });
        }

        // GET: api/delivery/drivers/available
        [HttpGet("drivers/available")]
        public async Task<IActionResult> GetAvailableDrivers([FromQuery] int branchId)
        {
            // Backfill any missing driver profiles from Identity Users in the "Driver" role
            var driverRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Driver");
            if (driverRole != null)
            {
                var driverUserIds = await _context.UserRoles
                    .Where(ur => ur.RoleId == driverRole.Id)
                    .Select(ur => ur.UserId)
                    .ToListAsync();

                var driverUsers = await _context.Users
                    .Where(u => driverUserIds.Contains(u.Id) && !u.IsDeleted && u.IsActive)
                    .ToListAsync();

                bool changesSaved = false;
                foreach (var user in driverUsers)
                {
                    var hasProfile = await _context.Set<Driver>().AnyAsync(d => d.UserId == user.Id);
                    if (!hasProfile)
                    {
                        var driver = new Driver
                        {
                            UserId = user.Id,
                            FullName = user.FullName ?? user.UserName ?? "سائق",
                            PhoneNumber = user.PhoneNumber ?? user.Email ?? "",
                            Status = DriverStatus.Idle,
                            BranchId = user.BranchId ?? branchId,
                            CreatedOn = DateTime.Now
                        };
                        _context.Set<Driver>().Add(driver);
                        changesSaved = true;
                    }
                }
                if (changesSaved)
                {
                    await _context.SaveChangesAsync();
                }
            }

            var drivers = await _orderService.GetAvailableDriversAsync(branchId);
            return Ok(drivers);
        }

        // PUT: api/delivery/{id}/assign-driver
        [HttpPut("{id}/assign-driver")]
        public async Task<IActionResult> AssignDriver(int id, [FromBody] AssignDriverRequest request)
        {
            await _orderService.AssignDriverAsync(id, request.DriverId);

            var order = await _context.Orders
                .Include(o => o.Driver)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order != null && order.Driver != null)
            {
                // Notify Customer
                await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusChanged", new
                {
                    orderId = order.Id,
                    status = OrderStatus.OutForDelivery.ToString(),
                    driverName = order.Driver.FullName,
                    driverPhone = order.Driver.PhoneNumber
                });

                // Notify Driver Group or Specific Driver connection if available
                await _hubContext.Clients.All.SendAsync("TripAssigned", new { orderId = order.Id, driverId = request.DriverId });
            }

            return Ok(new { message = "Driver assigned and order set to Out for Delivery" });
        }

        // POST: api/delivery/settlements/settle
        [HttpPost("settlements/settle")]
        public async Task<IActionResult> SettleDriverTrip([FromBody] SettleTripRequest request)
        {
            string cashierId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "SystemCashier";
            
            await _orderService.SettleDriverTripAsync(request.DriverId, request.OrderId, request.CollectedCash, cashierId, request.Notes);

            // Notify customer that it's Completed & Paid
            await _hubContext.Clients.Group($"order_{request.OrderId}").SendAsync("OrderStatusChanged", new { orderId = request.OrderId, status = OrderStatus.Completed.ToString() });

            return Ok(new { message = "Driver trip settled successfully" });
        }


        // ================= DRIVER FLOWS =================

        // GET: api/delivery/driver/trips
        [HttpGet("driver/trips")]
        public async Task<IActionResult> GetDriverTrips([FromQuery] int driverId)
        {
            var trips = await _context.Orders
                .Include(o => o.DeliveryAddress)
                .Where(o => o.DriverId == driverId && o.Status == OrderStatus.OutForDelivery)
                .ToListAsync();

            return Ok(trips);
        }

        // PUT: api/delivery/driver/status
        [HttpPut("driver/status")]
        public async Task<IActionResult> UpdateDriverStatus([FromBody] UpdateDriverStatusRequest request)
        {
            var driver = await _context.Drivers.FindAsync(request.DriverId);
            if (driver == null) return NotFound("Driver not found");

            driver.Status = request.Status;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Driver status updated successfully", status = driver.Status.ToString() });
        }

        // PUT: api/delivery/driver/orders/{orderId}/status
        [HttpPut("driver/orders/{orderId}/status")]
        public async Task<IActionResult> UpdateDeliveryOrderStatus(int orderId, [FromBody] UpdateDeliveryOrderStatusRequest request)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound("Order not found");

            if (order.Status != OrderStatus.OutForDelivery)
            {
                return BadRequest("Order is not out for delivery");
            }

            order.Status = request.Status; // Delivered or FailedDelivery
            if (request.Status == OrderStatus.FailedDelivery)
            {
                order.Note = $"{order.Note} | Delivery Failure: {request.FailureReason}";
            }
            await _context.SaveChangesAsync();

            // Notify customer and cashier dashboard
            await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("OrderStatusChanged", new { orderId = order.Id, status = order.Status.ToString(), failureReason = request.FailureReason });
            await _hubContext.Clients.Group($"cashier_{order.BranchId}").SendAsync("OrderStatusChanged", new { id = order.Id, status = order.Status.ToString() });

            return Ok(new { message = $"Order status updated to {order.Status}" });
        }

        // POST: api/delivery/driver/location
        [HttpPost("driver/location")]
        public async Task<IActionResult> UpdateDriverLocation([FromBody] DriverLocationRequest request)
        {
            // Broadcast driver coordinates to customer tracking group
            await _hubContext.Clients.Group($"order_{request.OrderId}").SendAsync("DriverLocationChanged", new
            {
                orderId = request.OrderId,
                latitude = request.Latitude,
                longitude = request.Longitude
            });

            return Ok();
        }

        // ================= HELPERS =================

        private decimal ResolvePrice(MenuItem menuItem, int branchId, string? priceCategory, int quantity, string? customerKey)
        {
            var now = DateTime.Now;
            var category = string.IsNullOrWhiteSpace(priceCategory) ? "Retail" : priceCategory;
            var price = menuItem.Prices
                .Where(p => p.IsActive && !p.IsDeleted)
                .Where(p => !p.StartsOn.HasValue || p.StartsOn.Value <= now)
                .Where(p => !p.EndsOn.HasValue || p.EndsOn.Value >= now)
                .Where(p => !p.BranchId.HasValue || p.BranchId.Value == branchId)
                .Where(p => !p.MinQuantity.HasValue || quantity >= p.MinQuantity.Value)
                .Where(p => string.IsNullOrWhiteSpace(p.CustomerKey) || p.CustomerKey == customerKey)
                .Where(p => p.PriceType == category || p.PriceType == "Retail")
                .OrderBy(p => p.PriceType == category ? 0 : 1)
                .ThenBy(p => p.BranchId.HasValue ? 0 : 1)
                .ThenBy(p => string.IsNullOrWhiteSpace(p.CustomerKey) ? 1 : 0)
                .ThenBy(p => p.Priority)
                .FirstOrDefault();

            return price?.Price ?? menuItem.Price;
        }
    }

    // ================= DTOs =================

    public class DeliveryCheckoutRequest
    {
        public int BranchId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DeliveryAddressRequest Address { get; set; } = null!;
        public List<DeliveryOrderItemRequest> Items { get; set; } = null!;
    }

    public class DeliveryAddressRequest
    {
        public string Governorate { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string Building { get; set; } = string.Empty;
        public string Floor { get; set; } = string.Empty;
        public string ApartmentNumber { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int DeliveryZoneId { get; set; }
        public string? DeliveryInstructions { get; set; }
    }

    public class DeliveryOrderItemRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
        public List<int>? AddOnIds { get; set; }
        public string? Size { get; set; }
    }

    public class RejectOrderRequest
    {
        public string? Reason { get; set; }
    }

    public class AssignDriverRequest
    {
        public int DriverId { get; set; }
    }

    public class SettleTripRequest
    {
        public int DriverId { get; set; }
        public int OrderId { get; set; }
        public decimal CollectedCash { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateDriverStatusRequest
    {
        public int DriverId { get; set; }
        public DriverStatus Status { get; set; }
    }

    public class UpdateDeliveryOrderStatusRequest
    {
        public OrderStatus Status { get; set; }
        public string? FailureReason { get; set; }
    }

    public class DriverLocationRequest
    {
        public int OrderId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
