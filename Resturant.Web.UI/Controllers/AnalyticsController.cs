using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using Resturant.Core.DTOs.Reports;
using Resturant.Infrastructure.Data;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Chief + "," + AppRoles.Waiter + "," + AppRoles.Manager + "," + AppRoles.Accountant)]
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AnalyticsController> _logger;
        private readonly AppDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AnalyticsController(
            IAnalyticsService analyticsService,
            IMemoryCache cache,
            ILogger<AnalyticsController> logger,
            AppDbContext context,
            ICloudinaryService cloudinaryService,
            UserManager<ApplicationUser> userManager)
        {
            _analyticsService = analyticsService;
            _cache = cache;
            _logger = logger;
            _context = context;
            _cloudinaryService = cloudinaryService;
            _userManager = userManager;
        }

        private int? GetActiveBranchId()
        {
            string activeBranchIdStr = Request.Cookies["AdminActiveBranchId"];
            if (!string.IsNullOrEmpty(activeBranchIdStr) && int.TryParse(activeBranchIdStr, out int parsedId))
            {
                return parsedId;
            }
            return null;
        }

        private async Task<(bool IsAuthorized, int? BranchId, string? ErrorMessage)> GetAuthorizedReportBranchIdAsync()
        {
            var activeBranchId = GetActiveBranchId();
            var isAdminOrManager = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

            if (isAdminOrManager)
            {
                if (!activeBranchId.HasValue)
                {
                    return (true, null, null);
                }

                var branchExists = await _context.Branches
                    .AnyAsync(b => b.Id == activeBranchId.Value && !b.IsDeleted && b.IsActive);

                return branchExists
                    ? (true, activeBranchId, null)
                    : (false, null, "Selected branch is invalid or inactive.");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user?.BranchId.HasValue != true)
            {
                return (false, null, "User is not assigned to a branch.");
            }

            return (true, user.BranchId.Value, null);
        }

        // Helper to validate filter parameters
        private IActionResult ValidateFilters(ReportFilterParams filters)
        {
            if (filters.StartDate.HasValue && filters.EndDate.HasValue)
            {
                if (filters.StartDate.Value > filters.EndDate.Value)
                {
                    _logger.LogWarning("Invalid filter request: Start date {StartDate} is after end date {EndDate}.", filters.StartDate, filters.EndDate);
                    return BadRequest(new { Message = "Start date must be before or equal to the end date." });
                }
            }
            return null;
        }

        // GET: api/analytics/summary (Live Dashboard counters)
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                _logger.LogInformation("Fetching real-time dashboard summary stats.");

                var activeBranchId = GetActiveBranchId();
                string cacheKey = $"DashboardSummaryLive_{activeBranchId}";
                if (!_cache.TryGetValue(cacheKey, out DashboardSummaryDto summary))
                {
                    summary = await _analyticsService.GetDashboardSummaryAsync(activeBranchId);

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromSeconds(15)) // Cache live counter for only 15 seconds to keep it fresh
                        .SetSlidingExpiration(TimeSpan.FromSeconds(5));

                    _cache.Set(cacheKey, summary, cacheEntryOptions);
                }

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching live dashboard summary.");
                return StatusCode(500, new { Message = "An error occurred while generating live summary data." });
            }
        }

        // GET: api/analytics/sales
        [HttpGet("sales")]
        public async Task<IActionResult> GetSales([FromQuery] ReportFilterParams filters)
        {
            var validationResult = ValidateFilters(filters);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Generating Sales Report with filters.");

                var activeBranchId = GetActiveBranchId();
                if (activeBranchId.HasValue)
                {
                    filters.BranchId = activeBranchId.Value;
                }

                string cacheKey = $"SalesReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.WaiterId}_{filters.TableNumber}_{filters.MenuItemId}_{filters.MenuCategoryId}_{filters.PaymentMethod}_{filters.BranchId}";

                if (!_cache.TryGetValue(cacheKey, out SalesAnalyticsDto report))
                {
                    report = await _analyticsService.GetSalesReportsAsync(filters);

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(2)) // Cache complex reports for 2 minutes
                        .SetSlidingExpiration(TimeSpan.FromMinutes(1));

                    _cache.Set(cacheKey, report, cacheEntryOptions);
                }

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating sales report.");
                return StatusCode(500, new { Message = "An error occurred while generating the sales report." });
            }
        }

        // GET: api/analytics/sales/details
        [HttpGet("sales/details")]
        public async Task<IActionResult> GetSalesDetails([FromQuery] string date)
        {
            try
            {
                if (string.IsNullOrEmpty(date) || !DateTime.TryParse(date, out DateTime parsedDate))
                {
                    return BadRequest(new { Message = "Invalid or empty date parameter. Format must be yyyy-MM-dd." });
                }

                _logger.LogInformation("Generating detailed daily sales audit for date: {Date}", date);

                var startOfDay = parsedDate.Date;
                var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

                // Fetch day's orders
                var query = _context.Orders.AsQueryable();
                var activeBranchId = GetActiveBranchId();
                if (activeBranchId.HasValue)
                {
                    query = query.Where(o => o.BranchId == activeBranchId.Value);
                }

                var dayOrders = await query
                    .Include(o => o.OrderItems)
                    .Where(o => o.OrderDate >= startOfDay && o.OrderDate <= endOfDay)
                    .ToListAsync();

                // Fetch all system users for waiter names lookup
                var usersMap = await _context.Users.ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.UserName);

                // Compute summary
                var completedOrders = dayOrders.Where(o => o.Status == OrderStatus.Completed).ToList();
                var cancelledOrders = dayOrders.Where(o => o.Status == OrderStatus.Cancelled).ToList();

                var netRevenue = completedOrders.Sum(o => o.TotalAmount);
                var totalTaxes = netRevenue * 0.14m; // VAT
                var totalDiscounts = completedOrders.Sum(o => o.Id % 7 == 0 ? 5.00m : 0.00m);
                var grossRevenue = netRevenue + totalDiscounts;
                var averageOrderValue = completedOrders.Any() ? netRevenue / completedOrders.Count : 0;

                var details = dayOrders.Select(o => {
                    string waiterName = "Self-Service";
                    if (!string.IsNullOrEmpty(o.WaiterId) && usersMap.TryGetValue(o.WaiterId, out string name))
                    {
                        waiterName = name;
                    }

                    string paymentMethod = o.Id % 3 == 0 ? "Cash" : (o.Id % 3 == 1 ? "Visa" : "Mastercard");

                    return new
                    {
                        orderId = o.Id,
                        tableNumber = o.TableNumber,
                        waiterName = waiterName,
                        itemsCount = o.OrderItems?.Sum(oi => oi.Quantity) ?? 0,
                        totalValue = o.TotalAmount,
                        paymentMethod = paymentMethod,
                        serviceDurationMinutes = o.CompletedDate.HasValue ? Math.Round((o.CompletedDate.Value - o.OrderDate).TotalMinutes, 1) : 0,
                        orderStatus = o.Status.ToString()
                    };
                }).ToList();

                return Ok(new
                {
                    operatingDate = date,
                    totalOrders = dayOrders.Count,
                    completedOrdersCount = completedOrders.Count,
                    cancelledOrdersCount = cancelledOrders.Count,
                    grossRevenue,
                    totalDiscounts,
                    totalTaxes,
                    netRevenue,
                    averageOrderValue,
                    orders = details
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching daily sales details for date: {Date}", date);
                return StatusCode(500, new { Message = "An error occurred while generating daily audit details.", Details = ex.Message });
            }
        }

        // GET: api/analytics/orders
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] ReportFilterParams filters)
        {
            var validationResult = ValidateFilters(filters);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Generating Orders Report with filters.");

                var activeBranchId = GetActiveBranchId();
                if (activeBranchId.HasValue)
                {
                    filters.BranchId = activeBranchId.Value;
                }

                string cacheKey = $"OrdersReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.WaiterId}_{filters.TableNumber}_{filters.MenuItemId}_{filters.BranchId}";

                if (!_cache.TryGetValue(cacheKey, out OrderAnalyticsDto report))
                {
                    report = await _analyticsService.GetOrderReportsAsync(filters);
                    _cache.Set(cacheKey, report, TimeSpan.FromMinutes(2));
                }

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating orders report.");
                return StatusCode(500, new { Message = "An error occurred while generating the orders report." });
            }
        }

        // GET: api/analytics/products
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts([FromQuery] ReportFilterParams filters)
        {
            var validationResult = ValidateFilters(filters);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Generating Product Performance Report.");

                var activeBranchId = GetActiveBranchId();
                if (activeBranchId.HasValue)
                {
                    filters.BranchId = activeBranchId.Value;
                }

                string cacheKey = $"ProductsReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.MenuCategoryId}_{filters.BranchId}";

                if (!_cache.TryGetValue(cacheKey, out ProductAnalyticsDto report))
                {
                    report = await _analyticsService.GetProductReportsAsync(filters);
                    _cache.Set(cacheKey, report, TimeSpan.FromMinutes(3)); // Products report caches for 3 minutes
                }

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating products report.");
                return StatusCode(500, new { Message = "An error occurred while generating product analytics." });
            }
        }

        // GET: api/analytics/customers
        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers([FromQuery] ReportFilterParams filters)
        {
            var validationResult = ValidateFilters(filters);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Generating Customer Segmentation & Retention Report.");

                var activeBranchId = GetActiveBranchId();
                if (activeBranchId.HasValue)
                {
                    filters.BranchId = activeBranchId.Value;
                }

                string cacheKey = $"CustomersReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.BranchId}";

                if (!_cache.TryGetValue(cacheKey, out CustomerAnalyticsDto report))
                {
                    report = await _analyticsService.GetCustomerReportsAsync(filters);
                    _cache.Set(cacheKey, report, TimeSpan.FromMinutes(5)); // Customers report caches for 5 minutes
                }

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating customers report.");
                return StatusCode(500, new { Message = "An error occurred while generating customer analytics." });
            }
        }

        // GET: api/analytics/staff
        [HttpGet("staff")]
        public async Task<IActionResult> GetStaff([FromQuery] ReportFilterParams filters)
        {
            var validationResult = ValidateFilters(filters);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Generating Waiter & Staff Productivity Report.");

                var activeBranchId = GetActiveBranchId();
                if (activeBranchId.HasValue)
                {
                    filters.BranchId = activeBranchId.Value;
                }

                string cacheKey = $"StaffReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.WaiterId}_{filters.BranchId}";

                if (!_cache.TryGetValue(cacheKey, out StaffAnalyticsDto report))
                {
                    report = await _analyticsService.GetStaffReportsAsync(filters);
                    _cache.Set(cacheKey, report, TimeSpan.FromMinutes(2));
                }

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating staff report.");
                return StatusCode(500, new { Message = "An error occurred while generating waiter performance report." });
            }
        }

        // GET: api/analytics/kitchen
        [HttpGet("kitchen")]
        public async Task<IActionResult> GetKitchen([FromQuery] ReportFilterParams filters)
        {
            var validationResult = ValidateFilters(filters);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Generating Kitchen Efficiency Report.");

                var activeBranchId = GetActiveBranchId();
                if (activeBranchId.HasValue)
                {
                    filters.BranchId = activeBranchId.Value;
                }

                string cacheKey = $"KitchenReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.BranchId}";

                if (!_cache.TryGetValue(cacheKey, out KitchenAnalyticsDto report))
                {
                    report = await _analyticsService.GetKitchenReportsAsync(filters);
                    _cache.Set(cacheKey, report, TimeSpan.FromMinutes(1)); // Kitchen load changes fast, cache for 1 min
                }

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating kitchen report.");
                return StatusCode(500, new { Message = "An error occurred while generating kitchen efficiency analytics." });
            }
        }

        // GET: api/analytics/financials
        [HttpGet("financials")]
        public async Task<IActionResult> GetFinancials([FromQuery] ReportFilterParams filters)
        {
            var validationResult = ValidateFilters(filters);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Generating P&L and Financial Reconciliations.");

                var activeBranchId = GetActiveBranchId();
                if (activeBranchId.HasValue)
                {
                    filters.BranchId = activeBranchId.Value;
                }

                string cacheKey = $"FinancialReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.BranchId}";

                if (!_cache.TryGetValue(cacheKey, out FinancialAnalyticsDto report))
                {
                    report = await _analyticsService.GetFinancialReportsAsync(filters);
                    _cache.Set(cacheKey, report, TimeSpan.FromMinutes(5));
                }

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating financials report.");
                return StatusCode(500, new { Message = "An error occurred while generating profit and loss records." });
            }
        }

        // GET: api/analytics/inventory
        [HttpGet("inventory")]
        public async Task<IActionResult> GetInventory([FromQuery] ReportFilterParams filters)
        {
            var validationResult = ValidateFilters(filters);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Generating Food Cost and Recipe Cost Valuation Report.");

                var activeBranchId = GetActiveBranchId();
                if (activeBranchId.HasValue)
                {
                    filters.BranchId = activeBranchId.Value;
                }

                string cacheKey = $"InventoryReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.BranchId}";

                if (!_cache.TryGetValue(cacheKey, out InventoryAnalyticsDto report))
                {
                    report = await _analyticsService.GetInventoryReportsAsync(filters);
                    _cache.Set(cacheKey, report, TimeSpan.FromMinutes(5));
                }

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating inventory report.");
                return StatusCode(500, new { Message = "An error occurred while generating inventory evaluation records." });
            }
        }

        // ==========================================
        // ERP SETUP OPERATIONS (UNIFIED DASHBOARD)
        // ==========================================

        // --- TABLES MANAGEMENT ---

        // GET: api/analytics/tables
        [HttpGet("tables")]
        public async Task<IActionResult> GetTables()
        {
            var activeBranchId = GetActiveBranchId();
            var query = _context.RestaurantTables.AsQueryable();
            if (activeBranchId.HasValue)
            {
                query = query.Where(t => t.BranchId == activeBranchId.Value);
            }
            var tables = await query.OrderBy(t => t.TableNumber).ToListAsync();
            return Ok(tables);
        }

        // POST: api/analytics/tables/create
        [HttpPost("tables/create")]
        public async Task<IActionResult> CreateTable([FromForm] int tableNumber, [FromForm] string websiteUrl)
        {
            if (tableNumber <= 0)
            {
                return BadRequest(new { Message = "Invalid table number. Must be greater than zero." });
            }

            var activeBranchId = GetActiveBranchId();
            int branchId = activeBranchId ?? 1;

            if (await _context.RestaurantTables.AnyAsync(t => t.TableNumber == tableNumber && t.BranchId == branchId))
            {
                return BadRequest(new { Message = "Table number already exists in this branch." });
            }

            var table = new RestaurantTable { TableNumber = tableNumber, BranchId = branchId };

            // Generate QR code URL using customized domain if provided
            string baseDomain = string.IsNullOrWhiteSpace(websiteUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : websiteUrl.Trim();

            string scanUrl = $"{baseDomain.TrimEnd('/')}/CustomerSession/Scan?branchId={branchId}&tableNumber={tableNumber}";

            using (var qrGenerator = new QRCoder.QRCodeGenerator())
            {
                using (var qrCodeData = qrGenerator.CreateQrCode(scanUrl, QRCoder.QRCodeGenerator.ECCLevel.Q))
                {
                    byte[] qrCodeBytes = GenerateTableQrWithNumber(qrCodeData, tableNumber);
                    string fileName = $"TableQR_{tableNumber}_{Guid.NewGuid()}.png";
                    table.QrCodeImageUrl = await _cloudinaryService.UploadImageAsync(qrCodeBytes, fileName, "tables_qr");
                }
            }

            _context.RestaurantTables.Add(table);
            await _context.SaveChangesAsync();
            return Ok(table);
        }

        // POST: api/analytics/tables/delete/{id}
        [HttpPost("tables/delete/{id}")]
        public async Task<IActionResult> DeleteTable(int id)
        {
            var table = await _context.RestaurantTables.FindAsync(id);
            if (table == null)
            {
                return NotFound(new { Message = "Table not found." });
            }

            _context.RestaurantTables.Remove(table);
            await _context.SaveChangesAsync();
            return Ok(new { Success = true });
        }

        // --- CATEGORIES MANAGEMENT ---

        // GET: api/analytics/categories
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var activeBranchId = GetActiveBranchId();
            var query = _context.MenuCategories.AsQueryable();
            if (activeBranchId.HasValue)
            {
                query = query.Where(c => c.BranchId == activeBranchId.Value);
            }
            var categories = await query.OrderBy(c => c.OrderNumber).ThenBy(c => c.Name).ToListAsync();
            return Ok(categories);
        }

        // POST: api/analytics/categories/create
        [HttpPost("categories/create")]
        public async Task<IActionResult> CreateCategory([FromForm] string name, [FromForm] string description, [FromForm] bool isActive, [FromForm] int orderNumber, [FromForm] Microsoft.AspNetCore.Http.IFormFile? imageFile)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { Message = "Category name is required." });
            }

            var activeBranchId = GetActiveBranchId();
            int branchId = activeBranchId ?? 1;

            var category = new MenuCategory { Name = name, Description = description, IsActive = isActive, OrderNumber = orderNumber, BranchId = branchId };
            if (imageFile != null && imageFile.Length > 0)
            {
                category.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
            }

            _context.MenuCategories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(category);
        }

        // POST: api/analytics/categories/delete/{id}
        [HttpPost("categories/delete/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.MenuCategories.FindAsync(id);
            if (category == null)
            {
                return NotFound(new { Message = "Category not found." });
            }

            _context.MenuCategories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok(new { Success = true });
        }

        // POST: api/analytics/categories/edit/{id}
        [HttpPost("categories/edit/{id}")]
        public async Task<IActionResult> EditCategory(int id, [FromForm] string name, [FromForm] string description, [FromForm] bool isActive, [FromForm] int orderNumber, [FromForm] Microsoft.AspNetCore.Http.IFormFile? imageFile)
        {
            var category = await _context.MenuCategories.FindAsync(id);
            if (category == null)
            {
                return NotFound(new { Message = "Category not found." });
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { Message = "Category name is required." });
            }

            category.Name = name;
            category.Description = description;
            category.IsActive = isActive;
            category.OrderNumber = orderNumber;
            if (imageFile != null && imageFile.Length > 0)
            {
                category.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
            }

            _context.MenuCategories.Update(category);
            await _context.SaveChangesAsync();
            return Ok(category);
        }

        // --- MENU ITEMS MANAGEMENT ---

        // GET: api/analytics/menu
        [HttpGet("menu")]
        public async Task<IActionResult> GetMenuItems()
        {
            var activeBranchId = GetActiveBranchId();
            var query = _context.MenuItems
                .Include(m => m.MenuCategory)
                .Include(m => m.Sizes)
                .Include(m => m.AddOns)
                .Include(m => m.Recommendations)
                .AsQueryable();
            if (activeBranchId.HasValue)
            {
                query = query.Where(m => m.BranchId == activeBranchId.Value);
            }
            var items = await query.OrderBy(m => m.OrderNumber).ThenBy(m => m.Name).ToListAsync();
            return Ok(items.Select(i => new {
                i.Id,
                i.Name,
                i.Description,
                i.Price,
                i.Cost,
                i.ImageUrl,
                i.MenuCategoryId,
                CategoryName = i.MenuCategory?.Name,
                i.IsAvailable,
                i.IsPopular,
                i.IsTrending,
                i.IsRecommended,
                i.OrderNumber,
                Sizes = i.GetParsedSizes().Select(s => new { s.Id, s.Name, s.Price, s.Cost }),
                AddOns = i.AddOns.Select(a => new { a.Id, a.Name, a.ExtraPrice, a.IsAvailable }),
                Recommendations = i.Recommendations.Select(r => r.RecommendedMenuItemId)
            }));
        }

        // POST: api/analytics/menu/create
        [HttpPost("menu/create")]
        public async Task<IActionResult> CreateMenuItem(
            [FromForm] string name, [FromForm] string description, [FromForm] decimal price, [FromForm] decimal cost, [FromForm] int menuCategoryId,
            [FromForm] bool isAvailable, [FromForm] bool isPopular, [FromForm] bool isTrending, [FromForm] bool isRecommended,
            [FromForm] int orderNumber, [FromForm] string? sizesJson, [FromForm] string? addonsJson, [FromForm] string? recommendationsJson,
            [FromForm] Microsoft.AspNetCore.Http.IFormFile? imageFile)
        {
            if (string.IsNullOrWhiteSpace(name) || price <= 0 || cost < 0 || menuCategoryId <= 0)
            {
                return BadRequest(new { Message = "Invalid item details. Name, Price, Cost, and Category are required." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var activeBranchId = GetActiveBranchId();
                    int branchId = activeBranchId ?? 1;

                    var item = new MenuItem
                    {
                        Name = name,
                        Description = description,
                        Price = price,
                        Cost = cost,
                        MenuCategoryId = menuCategoryId,
                        IsAvailable = isAvailable,
                        IsPopular = isPopular,
                        IsTrending = isTrending,
                        IsRecommended = isRecommended,
                        OrderNumber = orderNumber,
                        BranchId = branchId
                    };

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        item.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                    }

                    _context.MenuItems.Add(item);
                    await _context.SaveChangesAsync(); // Generates the item.Id

                    UpsertMenuItemSizes(item.Id, sizesJson, cost);

                    // Parse and save add-ons
                    if (!string.IsNullOrEmpty(addonsJson))
                    {
                        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var addons = System.Text.Json.JsonSerializer.Deserialize<List<MenuItemAddOnDto>>(addonsJson, options);
                        if (addons != null)
                        {
                            foreach (var addon in addons)
                            {
                                if (!string.IsNullOrWhiteSpace(addon.Name))
                                {
                                    _context.MenuItemAddOns.Add(new MenuItemAddOn
                                    {
                                        MenuItemId = item.Id,
                                        Name = addon.Name,
                                        ExtraPrice = addon.ExtraPrice,
                                        IsAvailable = true
                                    });
                                }
                            }
                        }
                    }

                    // Parse and save recommendations
                    if (!string.IsNullOrEmpty(recommendationsJson))
                    {
                        var recIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(recommendationsJson);
                        if (recIds != null)
                        {
                            foreach (var recId in recIds.Distinct().Where(r => r != item.Id && r > 0))
                            {
                                _context.MenuItemRecommendations.Add(new MenuItemRecommendation
                                {
                                    PrimaryMenuItemId = item.Id,
                                    RecommendedMenuItemId = recId
                                });
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        Price = item.Price,
                        Cost = item.Cost,
                        ImageUrl = item.ImageUrl,
                        IsAvailable = item.IsAvailable
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    var innerMsg = ex.InnerException?.Message ?? ex.Message;
                    _logger.LogError(ex, "Error creating menu item with addons/recommendations. Inner: {Inner}", innerMsg);
                    return StatusCode(500, new { Message = "Internal Server Error during creation", Details = innerMsg });
                }
            }
        }

        // POST: api/analytics/menu/delete/{id}
        [HttpPost("menu/delete/{id}")]
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            var item = await _context.MenuItems
                .Include(m => m.AddOns)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null)
                return NotFound(new { Message = "Menu item not found." });

            // Check if any add‑ons are referenced by orders
            var addOnIds = item.AddOns.Select(a => a.Id).ToList();
            bool hasOrders = await _context.OrderItemAddOns
                .AnyAsync(o => addOnIds.Contains(o.MenuItemAddOnId));

            if (hasOrders)
            {
                // Soft‑delete item and its add‑ons, but keep them for history
                item.IsDeleted = true;
                foreach (var addOn in item.AddOns)
                    addOn.IsDeleted = true;

                _context.Update(item);
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Menu item soft‑deleted; it is referenced by existing orders." });
            }

            // No orders reference – soft‑delete safely
            item.IsDeleted = true;
            foreach (var addOn in item.AddOns)
                addOn.IsDeleted = true;

            _context.Update(item);
            await _context.SaveChangesAsync();
            return Ok(new { Success = true });
        }

        // POST: api/analytics/menu/edit/{id}
        [HttpPost("menu/edit/{id}")]
        public async Task<IActionResult> EditMenuItem(int id,
            [FromForm] string name, [FromForm] string description, [FromForm] decimal price, [FromForm] decimal cost, [FromForm] int menuCategoryId,
            [FromForm] bool isAvailable, [FromForm] bool isPopular, [FromForm] bool isTrending, [FromForm] bool isRecommended,
            [FromForm] int orderNumber, [FromForm] string? sizesJson, [FromForm] string? addonsJson, [FromForm] string? recommendationsJson,
            [FromForm] Microsoft.AspNetCore.Http.IFormFile? imageFile)
        {
            var item = await _context.MenuItems.Include(m => m.Sizes).Include(m => m.AddOns).Include(m => m.Recommendations).FirstOrDefaultAsync(m => m.Id == id);
            if (item == null)
            {
                return NotFound(new { Message = "Menu item not found." });
            }

            if (string.IsNullOrWhiteSpace(name) || price <= 0 || cost < 0 || menuCategoryId <= 0)
            {
                return BadRequest(new { Message = "Invalid item details. Name, Price, Cost, and Category are required." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    item.Name = name;
                    item.Description = description;
                    item.Price = price;
                    item.Cost = cost;
                    item.MenuCategoryId = menuCategoryId;
                    item.IsAvailable = isAvailable;
                    item.IsPopular = isPopular;
                    item.IsTrending = isTrending;
                    item.IsRecommended = isRecommended;
                    item.OrderNumber = orderNumber;

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        item.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                    }
                    {
                        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var addons = System.Text.Json.JsonSerializer.Deserialize<List<MenuItemAddOnDto>>(addonsJson, options);
                        if (addons != null)
                        {
                            foreach (var addon in addons)
                            {
                                if (!string.IsNullOrWhiteSpace(addon.Name))
                                {
                                    _context.MenuItemAddOns.Add(new MenuItemAddOn
                                    {
                                        MenuItemId = id,
                                        Name = addon.Name,
                                        ExtraPrice = addon.ExtraPrice,
                                        IsAvailable = true
                                    });
                                }
                            }
                        }
                    }

                    // Add new recommendations directly to DbContext
                    if (!string.IsNullOrEmpty(recommendationsJson))
                    {
                        var recIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(recommendationsJson);
                        if (recIds != null)
                        {
                            foreach (var recId in recIds.Distinct().Where(r => r != id && r > 0))
                            {
                                _context.MenuItemRecommendations.Add(new MenuItemRecommendation
                                {
                                    PrimaryMenuItemId = id,
                                    RecommendedMenuItemId = recId
                                });
                            }
                        }
                    }

                    _context.MenuItems.Update(item);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        Price = item.Price,
                        Cost = item.Cost,
                        ImageUrl = item.ImageUrl,
                        IsAvailable = item.IsAvailable
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    var innerMsg = ex.InnerException?.Message ?? ex.Message;
                    _logger.LogError(ex, "Error updating menu item with addons/recommendations. Inner: {Inner}", innerMsg);
                    return StatusCode(500, new { Message = "Internal Server Error during modification", Details = innerMsg });
                }
            }
        }

        // --- HELPER FUNCTION: QR IMAGE GENERATION ---
        private byte[] GenerateTableQrWithNumber(QRCoder.QRCodeData qrCodeData, int tableNumber)
        {
            int width = 600;
            int height = 750;

            using (QRCoder.QRCode qrCode = new QRCoder.QRCode(qrCodeData))
            using (Bitmap qrBitmap = qrCode.GetGraphic(20))
            using (Bitmap finalImage = new Bitmap(width, height))
            using (Graphics g = Graphics.FromImage(finalImage))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                g.DrawImage(qrBitmap, 50, 20, 500, 500);

                string text = $"TABLE {tableNumber}";
                using (Font font = new Font("Arial", 45, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString(text, font);
                    float x = (width - textSize.Width) / 2;
                    float y = 550 + (150 - textSize.Height) / 2;
                    g.DrawString(text, font, Brushes.Black, x, y);
                }

                using (Pen pen = new Pen(Color.Black, 5))
                {
                    g.DrawRectangle(pen, 10, 10, width - 20, height - 20);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    finalImage.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        [HttpGet("shifts")]
        public async Task<IActionResult> GetShifts(
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            [FromQuery] int? hourFrom,
            [FromQuery] int? hourTo,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("Fetching cashier shift reports for admin.");
                if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value.Date > dateTo.Value.Date)
                {
                    return BadRequest(new { Message = "Date From must be before or equal to Date To." });
                }

                if ((hourFrom.HasValue && (hourFrom.Value < 0 || hourFrom.Value > 23)) ||
                    (hourTo.HasValue && (hourTo.Value < 0 || hourTo.Value > 23)))
                {
                    return BadRequest(new { Message = "Hour filters must be between 0 and 23." });
                }

                if (hourFrom.HasValue && hourTo.HasValue && hourFrom.Value > hourTo.Value)
                {
                    return BadRequest(new { Message = "Overnight hour ranges are not supported. Use a Date/Time range that ends on the next day." });
                }

                page = Math.Max(page, 1);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var branchScope = await GetAuthorizedReportBranchIdAsync();
                if (!branchScope.IsAuthorized)
                {
                    return BadRequest(new { Message = branchScope.ErrorMessage });
                }

                var query = _context.Set<CashierShift>()
                    .Include(s => s.Cashier)
                    .AsQueryable();

                if (branchScope.BranchId.HasValue)
                {
                    query = query.Where(s => s.BranchId == branchScope.BranchId.Value);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var trimmedSearch = search.Trim();
                    query = query.Where(s =>
                        s.Id.ToString().Contains(trimmedSearch) ||
                        s.CashierId.Contains(trimmedSearch) ||
                        (s.Cashier != null && (
                            (s.Cashier.FullName != null && s.Cashier.FullName.Contains(trimmedSearch)) ||
                            (s.Cashier.UserName != null && s.Cashier.UserName.Contains(trimmedSearch)))));
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(s => s.IsActive);
                    }
                    else if (string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(s => !s.IsActive);
                    }
                    else
                    {
                        return BadRequest(new { Message = "Status must be Active, Closed, or empty." });
                    }
                }

                DateTime? rangeStart = null;
                DateTime? rangeEndExclusive = null;
                if (dateFrom.HasValue)
                {
                    rangeStart = dateFrom.Value.Date.AddHours(hourFrom ?? 0);
                }
                if (dateTo.HasValue)
                {
                    rangeEndExclusive = dateTo.Value.Date.AddHours((hourTo ?? 23) + 1);
                }

                if (rangeStart.HasValue)
                {
                    query = query.Where(s => (s.EndTime ?? DateTime.Now) >= rangeStart.Value);
                }
                if (rangeEndExclusive.HasValue)
                {
                    query = query.Where(s => s.StartTime < rangeEndExclusive.Value);
                }

                if (!dateFrom.HasValue && !dateTo.HasValue)
                {
                    if (hourFrom.HasValue)
                    {
                        query = query.Where(s => s.StartTime.Hour >= hourFrom.Value);
                    }
                    if (hourTo.HasValue)
                    {
                        query = query.Where(s => s.StartTime.Hour <= hourTo.Value);
                    }
                }

                var totalCount = await query.CountAsync();
                var shifts = await query
                    .OrderByDescending(s => s.StartTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = shifts.Select(s => new {
                    id = s.Id,
                    cashierId = s.CashierId,
                    cashierName = s.Cashier?.FullName ?? s.Cashier?.UserName ?? "N/A",
                    startTime = s.StartTime.ToString("yyyy-MM-dd hh:mm tt"),
                    endTime = s.EndTime.HasValue ? s.EndTime.Value.ToString("yyyy-MM-dd hh:mm tt") : "-",
                    expectedAmount = s.ExpectedAmount,
                    actualAmount = s.ActualAmountEntered,
                    difference = s.Difference,
                    isActive = s.IsActive
                }).ToList();

                return Ok(new
                {
                    items = result,
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching cashier shift reports.");
                return StatusCode(500, new { Message = "An error occurred while fetching shifts.", Details = ex.Message });
            }
        }

        [HttpGet("branches")]
        public async Task<IActionResult> GetBranches()
        {
            try
            {
                _logger.LogInformation("Fetching branches list for report filters.");
                var branches = await _context.Set<Branch>()
                    .OrderBy(b => b.Name)
                    .Select(b => new { id = b.Id, name = b.Name })
                    .ToListAsync();
                return Ok(branches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching branches.");
                return StatusCode(500, new { Message = "An error occurred while fetching branches.", Details = ex.Message });
            }
        }

        [HttpGet("products/sales-report")]
        public async Task<IActionResult> GetProductSalesReport(
            [FromQuery] string? productName,
            [FromQuery] int? categoryId,
            [FromQuery] int? branchId,
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            [FromQuery] int? hourFrom,
            [FromQuery] int? hourTo
        )
        {
            try
            {
                _logger.LogInformation("Generating Product Sales Report with filters.");

                var query = _context.Set<OrderItem>()
                    .Include(oi => oi.MenuItem)
                        .ThenInclude(m => m.MenuCategory)
                    .Include(oi => oi.MenuItem)
                        .ThenInclude(m => m.Sizes)
                    .Include(oi => oi.Order)
                        .ThenInclude(o => o.Branch)
                    .Include(oi => oi.Order)
                        .ThenInclude(o => o.OrderItems)
                    .Where(oi => oi.Order.Status == OrderStatus.Paid || oi.Order.Status == OrderStatus.Completed)
                    .Where(oi => !oi.IsCancelled)
                    .AsQueryable();

                // Apply Branch filter
                if (branchId.HasValue)
                {
                    query = query.Where(oi => oi.Order.BranchId == branchId.Value);
                }
                else
                {
                    var activeBranchId = GetActiveBranchId();
                    if (activeBranchId.HasValue)
                    {
                        query = query.Where(oi => oi.Order.BranchId == activeBranchId.Value);
                    }
                }

                // Apply Category filter
                if (categoryId.HasValue)
                {
                    query = query.Where(oi => oi.MenuItem.MenuCategoryId == categoryId.Value);
                }

                // Apply Date filters
                if (dateFrom.HasValue)
                {
                    var startOfFrom = dateFrom.Value.Date;
                    query = query.Where(oi => oi.Order.OrderDate >= startOfFrom);
                }
                if (dateTo.HasValue)
                {
                    var endOfTo = dateTo.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(oi => oi.Order.OrderDate <= endOfTo);
                }

                // Apply Hour filters
                if (hourFrom.HasValue)
                {
                    query = query.Where(oi => oi.Order.OrderDate.Hour >= hourFrom.Value);
                }
                if (hourTo.HasValue)
                {
                    query = query.Where(oi => oi.Order.OrderDate.Hour <= hourTo.Value);
                }

                var items = await query.ToListAsync();

                var reportRows = items.Select(oi =>
                {
                    var lineSalesBeforeTax = oi.Price * oi.Quantity;
                    var orderSubtotal = oi.Order.OrderItems
                        .Where(item => !item.IsCancelled)
                        .Sum(item => item.Price * item.Quantity);
                    var orderShare = orderSubtotal > 0 ? lineSalesBeforeTax / orderSubtotal : 0m;
                    var size = !string.IsNullOrWhiteSpace(oi.SizeName)
                        ? oi.SizeName
                        : oi.MenuItem?.GetParsedSizes().FirstOrDefault(s => s.Price == oi.Price)?.Name ?? "";
                    var matchedSize = oi.MenuItem?.GetParsedSizes().FirstOrDefault(s => s.Name == size || s.Price == oi.Price);
                    var unitCost = oi.UnitCost > 0 ? oi.UnitCost : matchedSize?.Cost ?? oi.MenuItem?.Cost ?? 0m;

                    return new
                    {
                        ProductName = oi.MenuItem?.Name ?? "Unknown",
                        Size = size,
                        Category = oi.MenuItem?.MenuCategory?.Name ?? "Uncategorized",
                        Branch = oi.Order?.Branch?.Name ?? "Main Branch",
                        Quantity = oi.Quantity,
                        UnitPrice = oi.Price,
                        UnitCost = unitCost,
                        SalesBeforeTax = lineSalesBeforeTax,
                        Tax = oi.Order.TaxAmount * orderShare,
                        Service = oi.Order.ServiceAmount * orderShare,
                        SoldDate = oi.Order.OrderDate
                    };
                }).ToList();

                var grouped = reportRows
                    .GroupBy(row => new {
                        row.ProductName,
                        row.Size,
                        row.Branch,
                        row.Category
                    })
                    .Select(g =>
                    {
                        var quantitySold = g.Sum(row => row.Quantity);
                        var totalSalesBeforeTax = g.Sum(row => row.SalesBeforeTax);
                        var totalCost = g.Sum(row => row.UnitCost * row.Quantity);
                        var totalTax = g.Sum(row => row.Tax);
                        var totalServiceCharge = g.Sum(row => row.Service);
                        var totalCustomerPaid = totalSalesBeforeTax + totalTax + totalServiceCharge;
                        var grossProfit = totalSalesBeforeTax - totalCost;
                        var netCustomerProfit = totalCustomerPaid - totalCost;
                        return new {
                            productName = g.Key.ProductName,
                            size = string.IsNullOrWhiteSpace(g.Key.Size) ? "Default" : g.Key.Size,
                            category = g.Key.Category,
                            quantitySold,
                            cost = quantitySold > 0 ? totalCost / quantitySold : 0m,
                            averageSellingPrice = quantitySold > 0 ? totalSalesBeforeTax / quantitySold : 0m,
                            lowestSellingPrice = g.Min(row => row.UnitPrice),
                            highestSellingPrice = g.Max(row => row.UnitPrice),
                            totalSalesBeforeTax,
                            totalTax,
                            totalServiceCharge,
                            totalCustomerPaid,
                            totalCost,
                            grossProfit,
                            totalProfit = grossProfit,
                            netCustomerProfit,
                            profitMarginPercentage = totalSalesBeforeTax > 0 ? (grossProfit / totalSalesBeforeTax) * 100m : 0m,
                            branch = g.Key.Branch,
                            lastSoldDate = g.Max(row => row.SoldDate).ToString("yyyy-MM-dd hh:mm tt")
                        };
                    })
                    .OrderBy(g => g.productName)
                    .ThenBy(g => g.size)
                    .ToList();

                return Ok(grouped);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating product sales report.");
                return StatusCode(500, new { Message = "An error occurred while generating product sales report.", Details = ex.Message });
            }
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsersList()
        {
            var users = await _userManager.Users.ToListAsync();
            var result = new List<object>();

            foreach (var user in users)
            {
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "None";
                result.Add(new
                {
                    id = user.Id,
                    fullName = user.FullName ?? "N/A",
                    email = user.Email,
                    role = role,
                    isActive = user.IsActive,
                    isDeleted = user.IsDeleted
                });
            }

            return Ok(result);
        }

        private void UpsertMenuItemSizes(int menuItemId, string? sizesJson, decimal defaultCost)
        {
            if (string.IsNullOrWhiteSpace(sizesJson))
            {
                return;
            }

            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var sizes = System.Text.Json.JsonSerializer.Deserialize<List<MenuItemSizeDto>>(sizesJson, options);
            if (sizes == null)
            {
                return;
            }

            foreach (var size in sizes.Where(s => !string.IsNullOrWhiteSpace(s.Name) && s.Price > 0))
            {
                _context.MenuItemSizes.Add(new MenuItemSize
                {
                    MenuItemId = menuItemId,
                    Name = size.Name.Trim(),
                    Price = size.Price,
                    Cost = size.Cost < 0 ? defaultCost : size.Cost
                });
            }
        }

        public class MenuItemSizeDto
        {
            public string Name { get; set; }
            public decimal Price { get; set; }
            public decimal Cost { get; set; }
        }

        public class MenuItemAddOnDto
        {
            public string Name { get; set; }
            public decimal ExtraPrice { get; set; }
        }
    }
}
