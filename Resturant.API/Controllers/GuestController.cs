using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Resturant.API.Hubs;
using Resturant.API.Models;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Resturant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GuestController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<OrderHub> _hubContext;

        public GuestController(
            AppDbContext context,
            IConfiguration configuration,
            IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _configuration = configuration;
            _hubContext = hubContext;
        }

        // POST: api/guest/session/start
        [HttpPost("session/start")]
        [AllowAnonymous]
        public async Task<IActionResult> StartGuestSession([FromBody] GuestSessionStartRequest request)
        {
            if (request == null || request.TableNumber <= 0 || request.BranchId <= 0)
            {
                return BadRequest("Invalid request data. BranchId and TableNumber are required.");
            }

            if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest("CustomerName and PhoneNumber are required to start a guest session.");
            }

            // Verify table existence
            var table = await _context.RestaurantTables
                .FirstOrDefaultAsync(t => t.TableNumber == request.TableNumber && t.BranchId == request.BranchId);
            if (table == null)
            {
                return NotFound($"Table {request.TableNumber} was not found in branch {request.BranchId}.");
            }

            // Check if there is an active session
            var activeSession = await _context.Set<TableSession>()
                .FirstOrDefaultAsync(s => s.TableId == table.Id && s.IsActive);

            if (activeSession != null)
            {
                // Verify if details match the existing guest (allow reconnection)
                if (string.Equals(activeSession.CustomerName, request.CustomerName, StringComparison.OrdinalIgnoreCase) &&
                    activeSession.PhoneNumber == request.PhoneNumber)
                {
                    // Match found! Reissue the token for this session.
                    return IssueGuestToken(activeSession, table);
                }
                else
                {
                    return BadRequest("This table is currently occupied by another guest. Please verify your details or contact waitstaff.");
                }
            }

            // Table is vacant. Create a new active session
            var newSession = new TableSession
            {
                TableId = table.Id,
                CustomerName = request.CustomerName,
                PhoneNumber = request.PhoneNumber,
                StartTime = DateTime.Now,
                IsActive = true,
                BranchId = request.BranchId
            };

            _context.Set<TableSession>().Add(newSession);
            await _context.SaveChangesAsync();

            // Notify waitstaff of guest seating in real-time
            var notifyPayload = new 
            { 
                branchId = request.BranchId, 
                tableNumber = request.TableNumber, 
                customerName = request.CustomerName, 
                eventType = "GuestSeated" 
            };
            await _hubContext.Clients.Group($"waiter_{request.BranchId}").SendAsync("ReceiveWaiterUpdate", notifyPayload);
            await _hubContext.Clients.Group($"admin_{request.BranchId}").SendAsync("ReceiveWaiterUpdate", notifyPayload);

            return IssueGuestToken(newSession, table);
        }

        private IActionResult IssueGuestToken(TableSession session, RestaurantTable table)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtSecret = _configuration["Jwt:Secret"] ?? "SuperSecretKeyForResturantAppTokenAuthentication2026!#ResturantKeyForSigningSecurityKeyNeedsToBeVeryLong";
            var key = Encoding.ASCII.GetBytes(jwtSecret);

            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, session.CustomerName),
                new Claim(ClaimTypes.Role, "Guest"),
                new Claim("SessionId", session.Id.ToString()),
                new Claim("TableNumber", table.TableNumber.ToString()),
                new Claim("BranchId", session.BranchId.ToString()),
                new Claim("GuestPhone", session.PhoneNumber)
            };

            // Table sessions are valid for 12 hours
            var expiry = DateTime.UtcNow.AddHours(12);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(authClaims),
                Expires = expiry,
                Issuer = _configuration["Jwt:Issuer"] ?? "ResturantAPI",
                Audience = _configuration["Jwt:Audience"] ?? "ResturantMobileApp",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return Ok(new GuestSessionStartResponse
            {
                Token = tokenString,
                Expiration = expiry,
                SessionId = session.Id,
                TableNumber = table.TableNumber,
                BranchId = session.BranchId,
                CustomerName = session.CustomerName,
                PhoneNumber = session.PhoneNumber
            });
        }

        // GET: api/guest/menu
        [Authorize(Roles = "Guest")]
        [HttpGet("menu")]
        public async Task<IActionResult> GetMenu()
        {
            var branchIdClaim = User.FindFirst("BranchId")?.Value;
            if (!int.TryParse(branchIdClaim, out int branchId))
            {
                return BadRequest("Invalid guest session context.");
            }

            var categories = await _context.MenuCategories
                .Include(c => c.MenuItems)
                    .ThenInclude(m => m.Sizes)
                .Include(c => c.MenuItems)
                    .ThenInclude(m => m.AddOns)
                .Where(c => c.IsActive && c.BranchId == branchId && c.MenuItems.Any(mi => mi.IsAvailable))
                .OrderBy(c => c.OrderNumber)
                .ToListAsync();

            var menu = categories.Select(c => new
            {
                categoryId = c.Id,
                categoryName = c.Name,
                items = c.MenuItems.Where(mi => mi.IsAvailable && mi.BranchId == branchId).Select(mi => new
                {
                    id = mi.Id,
                    name = mi.Name,
                    price = mi.Price,
                    description = mi.Description,
                    sizes = mi.GetParsedSizes().Select(s => new { name = s.Name, price = s.Price }).ToList(),
                    addOns = mi.AddOns.Where(a => a.IsAvailable).Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        price = a.ExtraPrice
                    }).ToList()
                }).OrderBy(mi => mi.name).ToList()
            }).ToList();

            return Ok(menu);
        }

        // POST: api/guest/orders
        [Authorize(Roles = "Guest")]
        [HttpPost("orders")]
        public async Task<IActionResult> CreateOrder([FromBody] GuestCreateOrderRequest request)
        {
            if (request == null || request.OrderItems == null || request.OrderItems.Count == 0)
            {
                return BadRequest("Order items cannot be empty.");
            }

            // Extract session claims
            var sessionIdClaim = User.FindFirst("SessionId")?.Value;
            var tableNumberClaim = User.FindFirst("TableNumber")?.Value;
            var branchIdClaim = User.FindFirst("BranchId")?.Value;
            var guestName = User.Identity?.Name ?? "Guest";
            var guestPhone = User.FindFirst("GuestPhone")?.Value ?? "0000000000";

            if (!int.TryParse(sessionIdClaim, out int sessionId) ||
                !int.TryParse(tableNumberClaim, out int tableNumber) ||
                !int.TryParse(branchIdClaim, out int branchId))
            {
                return BadRequest("Invalid guest session context.");
            }

            // Verify session is active
            var activeSession = await _context.Set<TableSession>().FindAsync(sessionId);
            if (activeSession == null || !activeSession.IsActive)
            {
                return BadRequest("Your session has expired. Please scan the QR code to start a new session.");
            }

            // Create new order - Guest orders start as 'Pending'
            var order = new Order
            {
                TableNumber = tableNumber,
                TableSessionId = sessionId,
                Status = OrderStatus.Pending,
                OrderDate = DateTime.Now,
                Note = request.Note,
                CustomerName = guestName,
                PhoneNumber = guestPhone,
                PriceCategory = activeSession.PriceCategory ?? "Retail",
                BranchId = branchId
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            decimal totalAmount = 0;
            var orderItemsEntities = new List<OrderItem>();

            foreach (var itemRequest in request.OrderItems)
            {
                var menuItem = await _context.MenuItems
                    .Include(m => m.Sizes)
                    .Include(m => m.Prices)
                    .FirstOrDefaultAsync(m => m.Id == itemRequest.MenuItemId);

                if (menuItem == null || !menuItem.IsAvailable) continue;

                var resolvedPrice = ResolveMenuItemPrice(menuItem, branchId, order.PriceCategory, itemRequest.Quantity, guestPhone);
                
                var selectedSize = menuItem.GetParsedSizes().FirstOrDefault(s =>
                    (!string.IsNullOrWhiteSpace(itemRequest.Size) && s.Name == itemRequest.Size) ||
                    (string.IsNullOrWhiteSpace(itemRequest.Size) && s.Price == resolvedPrice));

                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    MenuItemId = itemRequest.MenuItemId,
                    Quantity = itemRequest.Quantity,
                    Price = resolvedPrice,
                    MenuItemSizeId = selectedSize?.Id,
                    SizeName = selectedSize?.Name ?? itemRequest.Size,
                    UnitCost = selectedSize?.Cost ?? menuItem.Cost
                };

                _context.OrderItems.Add(orderItem);

                // Add AddOns
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

                orderItemsEntities.Add(orderItem);
                totalAmount += (orderItem.Price + addOnsTotal) * orderItem.Quantity;
            }

            var settings = await _context.RestaurantSettings.FirstOrDefaultAsync(s => s.BranchId == branchId)
                           ?? new RestaurantSetting { TaxPercentage = 14, ServicePercentage = 12 };

            decimal serviceAmount = totalAmount * (settings.ServicePercentage / 100);
            decimal taxAmount = (totalAmount + serviceAmount) * (settings.TaxPercentage / 100);
            decimal grandTotal = totalAmount + serviceAmount + taxAmount;

            order.TaxPercentage = settings.TaxPercentage;
            order.ServicePercentage = settings.ServicePercentage;
            order.ServiceAmount = serviceAmount;
            order.TaxAmount = taxAmount;
            order.TotalAmount = grandTotal;

            await _context.SaveChangesAsync();

            // Prepare SignalR Data
            var orderData = new
            {
                id = order.Id,
                tableNumber = order.TableNumber,
                branchId = order.BranchId,
                status = order.Status.ToString(),
                totalAmount = order.TotalAmount,
                orderDate = order.OrderDate,
                note = order.Note,
                items = orderItemsEntities.Select(oi => new
                {
                    menuItemName = _context.MenuItems.Find(oi.MenuItemId)?.GetFormattedNameWithSize(oi.SizeName, oi.Price) ?? "Unknown",
                    quantity = oi.Quantity,
                    price = oi.Price + oi.AddOns.Sum(a => a.Price),
                    total = oi.Total,
                    addOns = oi.AddOns.Select(a => new {
                        name = _context.MenuItemAddOns.Find(a.MenuItemAddOnId)?.Name ?? ""
                    }).ToList()
                }).ToList()
            };

            // Broadcast real-time notifications to staff (Waiters and Cashiers need to approve the pending order)
            await _hubContext.Clients.Group($"waiter_{branchId}").SendAsync("NewOrderReceived", orderData);
            await _hubContext.Clients.Group($"cashier_{branchId}").SendAsync("NewOrderReceived", orderData);
            await _hubContext.Clients.Group($"admin_{branchId}").SendAsync("NewOrderReceived", orderData);

            return Ok(new OrderResponse
            {
                OrderId = order.Id,
                TableNumber = order.TableNumber ?? 0,
                SessionId = order.TableSessionId,
                CustomerName = order.CustomerName ?? "",
                PhoneNumber = order.PhoneNumber ?? "",
                TotalAmount = order.TotalAmount,
                Status = order.Status.ToString(),
                OrderDate = order.OrderDate,
                Note = order.Note,
                Items = orderItemsEntities.Select(oi => new OrderItemResponse
                {
                    Id = oi.Id,
                    Name = _context.MenuItems.Find(oi.MenuItemId)?.Name ?? "Item",
                    Quantity = oi.Quantity,
                    Price = oi.Price,
                    IsCancelled = oi.IsCancelled,
                    AddOns = string.Join(", ", oi.AddOns.Select(a => _context.MenuItemAddOns.Find(a.MenuItemAddOnId)?.Name ?? ""))
                }).ToList()
            });
        }

        // POST: api/guest/call-waiter
        [Authorize(Roles = "Guest")]
        [HttpPost("call-waiter")]
        public async Task<IActionResult> CallWaiter()
        {
            var tableNumberClaim = User.FindFirst("TableNumber")?.Value;
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(tableNumberClaim, out int tableNumber) ||
                !int.TryParse(branchIdClaim, out int branchId))
            {
                return BadRequest("Invalid guest session context.");
            }

            var notifyData = new { tableNumber = tableNumber };
            await _hubContext.Clients.Group($"waiter_{branchId}").SendAsync("CallWaiterReceived", notifyData);
            await _hubContext.Clients.Group($"admin_{branchId}").SendAsync("CallWaiterReceived", notifyData);

            return Ok(new { message = "Waitstaff has been called to your table." });
        }

        // GET: api/guest/orders
        [Authorize(Roles = "Guest")]
        [HttpGet("orders")]
        public async Task<IActionResult> GetSessionOrders()
        {
            var sessionIdClaim = User.FindFirst("SessionId")?.Value;
            if (!int.TryParse(sessionIdClaim, out int sessionId))
            {
                return BadRequest("Invalid guest session context.");
            }

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.TableSessionId == sessionId && o.Status != OrderStatus.Cancelled)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var result = orders.Select(o => new OrderResponse
            {
                OrderId = o.Id,
                TableNumber = o.TableNumber ?? 0,
                SessionId = o.TableSessionId,
                CustomerName = o.CustomerName ?? "",
                PhoneNumber = o.PhoneNumber ?? "",
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                OrderDate = o.OrderDate,
                Note = o.Note,
                Items = o.OrderItems.Select(oi => new OrderItemResponse
                {
                    Id = oi.Id,
                    Name = oi.MenuItem?.Name ?? "Item",
                    Quantity = oi.Quantity,
                    Price = oi.Price,
                    IsCancelled = oi.IsCancelled,
                    AddOns = string.Join(", ", oi.AddOns.Select(a => _context.MenuItemAddOns.Find(a.MenuItemAddOnId)?.Name ?? ""))
                }).ToList()
            }).ToList();

            return Ok(result);
        }

        // GET: api/guest/orders/{orderId}/track
        [Authorize(Roles = "Guest")]
        [HttpGet("orders/{orderId}/track")]
        public async Task<IActionResult> TrackOrder(int orderId)
        {
            var sessionIdClaim = User.FindFirst("SessionId")?.Value;
            if (!int.TryParse(sessionIdClaim, out int sessionId))
            {
                return BadRequest("Invalid guest session context.");
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.TableSessionId == sessionId);

            if (order == null)
            {
                return NotFound("Order not found in your session.");
            }

            var response = new OrderResponse
            {
                OrderId = order.Id,
                TableNumber = order.TableNumber ?? 0,
                SessionId = order.TableSessionId,
                CustomerName = order.CustomerName ?? "",
                PhoneNumber = order.PhoneNumber ?? "",
                TotalAmount = order.TotalAmount,
                Status = order.Status.ToString(),
                OrderDate = order.OrderDate,
                Note = order.Note,
                Items = order.OrderItems.Select(oi => new OrderItemResponse
                {
                    Id = oi.Id,
                    Name = oi.MenuItem?.Name ?? "Item",
                    Quantity = oi.Quantity,
                    Price = oi.Price,
                    IsCancelled = oi.IsCancelled,
                    AddOns = string.Join(", ", oi.AddOns.Select(a => _context.MenuItemAddOns.Find(a.MenuItemAddOnId)?.Name ?? ""))
                }).ToList()
            };

            return Ok(response);
        }

        // Helper Method for Pricing
        private static decimal ResolveMenuItemPrice(MenuItem menuItem, int branchId, string? priceCategory, int quantity, string? customerKey)
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
}
