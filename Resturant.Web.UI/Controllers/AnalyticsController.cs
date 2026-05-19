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
                
                string cacheKey = "DashboardSummaryLive";
                if (!_cache.TryGetValue(cacheKey, out DashboardSummaryDto summary))
                {
                    summary = await _analyticsService.GetDashboardSummaryAsync();
                    
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
                
                string cacheKey = $"SalesReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.WaiterId}_{filters.TableNumber}_{filters.MenuItemId}_{filters.MenuCategoryId}_{filters.PaymentMethod}";
                
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

        // GET: api/analytics/orders
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] ReportFilterParams filters)
        {
            var validationResult = ValidateFilters(filters);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Generating Orders Report with filters.");
                
                string cacheKey = $"OrdersReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.WaiterId}_{filters.TableNumber}_{filters.MenuItemId}";
                
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
                
                string cacheKey = $"ProductsReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.MenuCategoryId}";
                
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
                
                string cacheKey = $"CustomersReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}";
                
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
                
                string cacheKey = $"StaffReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}_{filters.WaiterId}";
                
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
                
                string cacheKey = $"KitchenReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}";
                
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
                
                string cacheKey = $"FinancialReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}";
                
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
                
                string cacheKey = $"InventoryReport_{filters.StartDate:yyyyMMdd}_{filters.EndDate:yyyyMMdd}";
                
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
            var tables = await _context.RestaurantTables.OrderBy(t => t.TableNumber).ToListAsync();
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

            if (await _context.RestaurantTables.AnyAsync(t => t.TableNumber == tableNumber))
            {
                return BadRequest(new { Message = "Table number already exists in your restaurant." });
            }

            var table = new RestaurantTable { TableNumber = tableNumber };

            // Generate QR code URL using customized domain if provided
            string baseDomain = string.IsNullOrWhiteSpace(websiteUrl) 
                ? $"{Request.Scheme}://{Request.Host}" 
                : websiteUrl.Trim();
                
            string scanUrl = $"{baseDomain.TrimEnd('/')}/CustomerSession/Scan?tableNumber={tableNumber}";

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
            var categories = await _context.MenuCategories.OrderBy(c => c.OrderNumber).ThenBy(c => c.Name).ToListAsync();
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

            var category = new MenuCategory { Name = name, Description = description, IsActive = isActive, OrderNumber = orderNumber };
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
            var items = await _context.MenuItems
                .Include(m => m.MenuCategory)
                .Include(m => m.AddOns)
                .Include(m => m.Recommendations)
                .OrderBy(m => m.OrderNumber).ThenBy(m => m.Name).ToListAsync();
            return Ok(items.Select(i => new {
                i.Id,
                i.Name,
                i.Description,
                i.Price,
                i.ImageUrl,
                i.MenuCategoryId,
                CategoryName = i.MenuCategory?.Name,
                i.IsAvailable,
                i.IsPopular,
                i.IsTrending,
                i.IsRecommended,
                i.OrderNumber,
                AddOns = i.AddOns.Select(a => new { a.Id, a.Name, a.ExtraPrice, a.IsAvailable }),
                Recommendations = i.Recommendations.Select(r => r.RecommendedMenuItemId)
            }));
        }

        // POST: api/analytics/menu/create
        [HttpPost("menu/create")]
        public async Task<IActionResult> CreateMenuItem(
            [FromForm] string name, [FromForm] string description, [FromForm] decimal price, [FromForm] int menuCategoryId,
            [FromForm] bool isAvailable, [FromForm] bool isPopular, [FromForm] bool isTrending, [FromForm] bool isRecommended,
            [FromForm] int orderNumber, [FromForm] string? addonsJson, [FromForm] string? recommendationsJson,
            [FromForm] Microsoft.AspNetCore.Http.IFormFile? imageFile)
        {
            if (string.IsNullOrWhiteSpace(name) || price <= 0 || menuCategoryId <= 0)
            {
                return BadRequest(new { Message = "Invalid item details. Name, Price, and Category are required." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var item = new MenuItem
                    {
                        Name = name,
                        Description = description,
                        Price = price,
                        MenuCategoryId = menuCategoryId,
                        IsAvailable = isAvailable,
                        IsPopular = isPopular,
                        IsTrending = isTrending,
                        IsRecommended = isRecommended,
                        OrderNumber = orderNumber
                    };

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        item.ImageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                    }

                    _context.MenuItems.Add(item);
                    await _context.SaveChangesAsync(); // Generates the item.Id

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
                            foreach (var recId in recIds)
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
                        ImageUrl = item.ImageUrl, 
                        IsAvailable = item.IsAvailable 
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating menu item with addons/recommendations");
                    return StatusCode(500, new { Message = "Internal Server Error during creation", Details = ex.Message });
                }
            }
        }

        // POST: api/analytics/menu/delete/{id}
        [HttpPost("menu/delete/{id}")]
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null)
            {
                return NotFound(new { Message = "Menu item not found." });
            }

            // Clear relationships to avoid foreign key constraints issues
            var existingAddOns = await _context.MenuItemAddOns.Where(a => a.MenuItemId == id).ToListAsync();
            _context.MenuItemAddOns.RemoveRange(existingAddOns);

            var existingRecs = await _context.MenuItemRecommendations.Where(r => r.PrimaryMenuItemId == id || r.RecommendedMenuItemId == id).ToListAsync();
            _context.MenuItemRecommendations.RemoveRange(existingRecs);

            _context.MenuItems.Remove(item);
            await _context.SaveChangesAsync();
            return Ok(new { Success = true });
        }

        // POST: api/analytics/menu/edit/{id}
        [HttpPost("menu/edit/{id}")]
        public async Task<IActionResult> EditMenuItem(int id,
            [FromForm] string name, [FromForm] string description, [FromForm] decimal price, [FromForm] int menuCategoryId,
            [FromForm] bool isAvailable, [FromForm] bool isPopular, [FromForm] bool isTrending, [FromForm] bool isRecommended,
            [FromForm] int orderNumber, [FromForm] string? addonsJson, [FromForm] string? recommendationsJson,
            [FromForm] Microsoft.AspNetCore.Http.IFormFile? imageFile)
        {
            var item = await _context.MenuItems.Include(m => m.AddOns).Include(m => m.Recommendations).FirstOrDefaultAsync(m => m.Id == id);
            if (item == null)
            {
                return NotFound(new { Message = "Menu item not found." });
            }

            if (string.IsNullOrWhiteSpace(name) || price <= 0 || menuCategoryId <= 0)
            {
                return BadRequest(new { Message = "Invalid item details. Name, Price, and Category are required." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    item.Name = name;
                    item.Description = description;
                    item.Price = price;
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

                    // Clear existing child entities from the database context first
                    var existingAddOns = await _context.MenuItemAddOns.Where(a => a.MenuItemId == id).ToListAsync();
                    _context.MenuItemAddOns.RemoveRange(existingAddOns);

                    var existingRecs = await _context.MenuItemRecommendations.Where(r => r.PrimaryMenuItemId == id).ToListAsync();
                    _context.MenuItemRecommendations.RemoveRange(existingRecs);

                    // Clear the local navigation collections so EF doesn't get confused or try to track deleted entities
                    item.AddOns.Clear();
                    item.Recommendations.Clear();

                    await _context.SaveChangesAsync(); // Flush deletions to the database first!

                    // Add new add-ons directly to DbContext
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
                            foreach (var recId in recIds)
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
                        ImageUrl = item.ImageUrl, 
                        IsAvailable = item.IsAvailable 
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error updating menu item with addons/recommendations");
                    return StatusCode(500, new { Message = "Internal Server Error during modification", Details = ex.Message });
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

        public class MenuItemAddOnDto
        {
            public string Name { get; set; }
            public decimal ExtraPrice { get; set; }
        }
    }
}
