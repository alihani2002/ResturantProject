using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using Resturant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Manager + "," + AppRoles.Accountant)]
    public class InventoryController : Controller
    {
        private readonly IInventoryService _inventoryService;
        private readonly AppDbContext _context;

        public InventoryController(IInventoryService inventoryService, AppDbContext context)
        {
            _inventoryService = inventoryService;
            _context = context;
        }

        // --- View Handlers ---
        
        public async Task<IActionResult> Index()
        {
            ViewBag.Branches = await _context.Branches.Where(b => !b.IsDeleted && b.IsActive).ToListAsync();
            ViewBag.ActiveBranchId = Request.Cookies["AdminActiveBranchId"];
            return View();
        }

        // --- Ingredients API ---

        [HttpGet]
        public async Task<IActionResult> GetStockLevels(int? branchId)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            var stock = await _inventoryService.GetStockLevelsAsync(activeBranchId);
            return Json(stock.Select(i => new
            {
                i.Id,
                i.BranchId,
                BranchName = i.Branch?.Name,
                i.Name,
                i.CurrentStock,
                i.ReorderLevel,
                i.Unit,
                i.CostPerUnit,
                i.SupplierId,
                SupplierName = i.Supplier?.Name
            }));
        }

        [HttpPost]
        public async Task<IActionResult> AddStock([FromBody] AddStockRequest request)
        {
            if (request == null || request.IngredientId <= 0 || request.Quantity <= 0)
            {
                return BadRequest("Invalid stock addition data.");
            }

            try
            {
                var user = User.Identity?.Name ?? "Admin";
                var result = await _inventoryService.AddStockAsync(request.BranchId, request.IngredientId, request.Quantity, request.Cost, request.Notes, user);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> AdjustStock([FromBody] AdjustStockRequest request)
        {
            if (request == null || request.IngredientId <= 0 || string.IsNullOrEmpty(request.Type))
            {
                return BadRequest("Invalid stock adjustment data.");
            }

            try
            {
                var user = User.Identity?.Name ?? "Admin";
                var result = await _inventoryService.AdjustStockAsync(request.BranchId, request.IngredientId, request.QuantityAdjusted, request.Type, request.Notes, user);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // --- Stock Transfers API ---

        [HttpGet]
        public async Task<IActionResult> GetTransfers(int? branchId)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            var transfers = await _inventoryService.GetTransfersAsync(activeBranchId);
            return Json(transfers.Select(t => new {
                t.Id,
                t.SourceBranchId,
                SourceBranchName = t.SourceBranch?.Name ?? $"Branch {t.SourceBranchId}",
                t.DestinationBranchId,
                DestinationBranchName = t.DestinationBranch?.Name ?? $"Branch {t.DestinationBranchId}",
                t.Status,
                t.Notes,
                t.CreatedBy,
                CreatedOn = t.CreatedOn.ToString("yyyy-MM-dd HH:mm"),
                t.ProcessedBy,
                ProcessedDate = t.ProcessedDate?.ToString("yyyy-MM-dd HH:mm"),
                t.ReceivedBy,
                ReceivedDate = t.ReceivedDate?.ToString("yyyy-MM-dd HH:mm"),
                Items = t.Items.Select(i => new {
                    i.IngredientName,
                    i.Quantity,
                    i.Unit
                }).ToList()
            }));
        }

        [HttpPost]
        public async Task<IActionResult> RequestTransfer([FromBody] RequestTransferRequest request)
        {
            if (request == null || request.SourceBranchId <= 0 || request.DestinationBranchId <= 0 || request.Items == null || !request.Items.Any())
            {
                return BadRequest("Invalid stock transfer request data.");
            }

            try
            {
                var user = User.Identity?.Name ?? "Admin";
                var itemsDto = request.Items.Select(i => new StockTransferItemDto {
                    IngredientName = i.IngredientName,
                    Quantity = i.Quantity,
                    Unit = i.Unit
                }).ToList();

                var transfer = await _inventoryService.RequestTransferAsync(request.SourceBranchId, request.DestinationBranchId, itemsDto, request.Notes, user);
                return Ok(new { success = true, id = transfer.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveTransfer(int transferId)
        {
            try
            {
                var user = User.Identity?.Name ?? "Admin";
                var result = await _inventoryService.ApproveTransferAsync(transferId, user);
                return Ok(new { success = true, id = result.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveTransfer(int transferId)
        {
            try
            {
                var user = User.Identity?.Name ?? "Admin";
                var result = await _inventoryService.ReceiveTransferAsync(transferId, user);
                return Ok(new { success = true, id = result.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelTransfer(int transferId)
        {
            try
            {
                var user = User.Identity?.Name ?? "Admin";
                var result = await _inventoryService.CancelTransferAsync(transferId, user);
                return Ok(new { success = true, id = result.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // --- Recipes API ---

        [HttpGet]
        public async Task<IActionResult> GetRecipes(int menuItemId)
        {
            var recipes = await _inventoryService.GetRecipesForMenuItemAsync(menuItemId);
            return Json(recipes.Select(r => new {
                r.Id,
                r.MenuItemId,
                r.IngredientId,
                IngredientName = r.Ingredient?.Name,
                r.QuantityRequired,
                Unit = r.Ingredient?.Unit ?? "Kg"
            }));
        }

        [HttpPost]
        public async Task<IActionResult> SaveRecipe([FromBody] SaveRecipeRequest request)
        {
            if (request == null || request.MenuItemId <= 0)
            {
                return BadRequest("Invalid recipe save data.");
            }

            try
            {
                var itemsDto = request.Items.Select(i => new RecipeItemDto {
                    IngredientName = i.IngredientName,
                    QuantityRequired = i.QuantityRequired,
                    Unit = i.Unit
                }).ToList();

                await _inventoryService.SaveRecipeAsync(request.MenuItemId, itemsDto);
                return Ok(new { success = true, message = "Recipe saved successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBranches()
        {
            var branches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
            return Json(branches.Select(b => new { b.Id, b.Name, b.Address }));
        }

        [HttpGet]
        public async Task<IActionResult> GetMenuItems(int? branchId)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            var items = await _context.MenuItems.Where(m => m.BranchId == activeBranchId && m.IsAvailable).ToListAsync();
            return Json(items.Select(i => new { i.Id, i.Name, i.Price }));
        }

        [HttpGet]
        public async Task<IActionResult> GetSuppliers(int? branchId)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            var suppliers = await _inventoryService.GetSuppliersAsync(activeBranchId);

            return Json(suppliers.Select(s => new {
                s.Id,
                s.Name,
                s.BranchId,
                BranchName = s.Branch?.Name,
                s.Phone,
                s.Email,
                s.Address,
                s.LeadTimeDays,
                s.QualityRating
            }));
        }

        [HttpPost]
        public async Task<IActionResult> SaveSupplier([FromBody] SaveSupplierRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.BranchId <= 0)
            {
                return BadRequest("Invalid supplier data.");
            }

            var supplier = new Supplier
            {
                Id = request.Id,
                BranchId = request.BranchId,
                Name = request.Name.Trim(),
                Phone = request.Phone,
                Email = request.Email,
                Address = request.Address,
                LeadTimeDays = request.LeadTimeDays,
                QualityRating = request.QualityRating
            };

            var saved = await _inventoryService.SaveSupplierAsync(supplier);
            return Ok(saved);
        }

        [HttpGet]
        public async Task<IActionResult> GetPurchaseOrders(int? branchId, string? status)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            var orders = await _inventoryService.GetPurchaseOrdersAsync(activeBranchId, status);
            return Json(orders.Select(p => new {
                p.Id,
                p.OrderNumber,
                p.BranchId,
                BranchName = p.Branch?.Name,
                p.SupplierId,
                SupplierName = p.Supplier?.Name,
                p.Status,
                p.TotalAmount,
                OrderDate = p.OrderDate.ToString("yyyy-MM-dd HH:mm"),
                ExpectedDate = p.ExpectedDate?.ToString("yyyy-MM-dd"),
                ReceivedDate = p.ReceivedDate?.ToString("yyyy-MM-dd HH:mm"),
                p.CreatedBy,
                p.ApprovedBy,
                p.ReceivedBy,
                p.Notes,
                Items = p.Items.Select(i => new {
                    i.Id,
                    i.IngredientId,
                    IngredientName = i.Ingredient?.Name,
                    Unit = i.Ingredient?.Unit,
                    i.QuantityOrdered,
                    i.QuantityReceived,
                    i.UnitCost,
                    LineTotal = i.QuantityOrdered * (double)i.UnitCost
                })
            }));
        }

        [HttpPost]
        public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest request)
        {
            if (request == null || request.BranchId <= 0 || request.SupplierId <= 0 || request.Items == null || !request.Items.Any())
            {
                return BadRequest("Invalid purchase order data.");
            }

            try
            {
                var user = User.Identity?.Name ?? "Admin";
                var items = request.Items.Select(i => new PurchaseOrderItemDto
                {
                    IngredientId = i.IngredientId,
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost
                }).ToList();
                var po = await _inventoryService.CreatePurchaseOrderAsync(request.BranchId, request.SupplierId, items, request.ExpectedDate, request.Notes, user);
                return Ok(new { success = true, id = po.Id, po.OrderNumber });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePurchaseOrderStatus(int id, string status)
        {
            try
            {
                var po = await _inventoryService.UpdatePurchaseOrderStatusAsync(id, status, User.Identity?.Name ?? "Admin");
                return Ok(new { success = true, po.Id, po.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReceivePurchaseOrder([FromBody] ReceivePurchaseOrderRequest request)
        {
            if (request == null || request.Id <= 0) return BadRequest("Invalid receiving request.");

            try
            {
                var items = request.Items?.Select(i => new PurchaseOrderReceiveItemDto
                {
                    PurchaseOrderItemId = i.PurchaseOrderItemId,
                    QuantityReceived = i.QuantityReceived
                }).ToList();
                var po = await _inventoryService.ReceivePurchaseOrderAsync(request.Id, items, User.Identity?.Name ?? "Admin");
                return Ok(new { success = true, po.Id, po.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMovements(int? branchId, int? ingredientId, string? movementType, string? search, DateTime? startDate, DateTime? endDate, int page = 1, int pageSize = 25)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            var result = await _inventoryService.GetMovementsAsync(new InventoryMovementFilter
            {
                BranchId = activeBranchId,
                IngredientId = ingredientId,
                MovementType = movementType,
                Search = search,
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize
            });

            return Json(new {
                totalCount = result.TotalCount,
                page,
                pageSize,
                items = result.Items.Select(m => new {
                    m.Id,
                    m.MovementType,
                    m.IngredientId,
                    IngredientName = m.Ingredient?.Name,
                    Unit = m.Ingredient?.Unit,
                    m.Quantity,
                    m.StockBefore,
                    m.StockAfter,
                    m.BranchId,
                    BranchName = m.Branch?.Name,
                    m.UserName,
                    m.ReferenceNumber,
                    m.Notes,
                    m.UnitCost,
                    m.TotalCost,
                    MovementDate = m.MovementDate.ToString("yyyy-MM-dd HH:mm")
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportMovementsExcel(int? branchId, string? movementType, string? search, DateTime? startDate, DateTime? endDate)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            var result = await _inventoryService.GetMovementsAsync(new InventoryMovementFilter
            {
                BranchId = activeBranchId,
                MovementType = movementType,
                Search = search,
                StartDate = startDate,
                EndDate = endDate,
                Page = 1,
                PageSize = 2000
            });

            var sb = new StringBuilder();
            sb.AppendLine("Date,Type,Ingredient,Quantity,Stock Before,Stock After,Branch,User,Reference,Notes");
            foreach (var m in result.Items)
            {
                sb.AppendLine($"{m.MovementDate:yyyy-MM-dd HH:mm},{m.MovementType},{Csv(m.Ingredient?.Name)},{m.Quantity},{m.StockBefore},{m.StockAfter},{Csv(m.Branch?.Name)},{Csv(m.UserName)},{Csv(m.ReferenceNumber)},{Csv(m.Notes)}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "application/vnd.ms-excel", "inventory-movements.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportMovementsPdf(int? branchId, string? movementType, string? search, DateTime? startDate, DateTime? endDate)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            var result = await _inventoryService.GetMovementsAsync(new InventoryMovementFilter
            {
                BranchId = activeBranchId,
                MovementType = movementType,
                Search = search,
                StartDate = startDate,
                EndDate = endDate,
                Page = 1,
                PageSize = 2000
            });

            var text = "Inventory Movement History\n\n" + string.Join("\n", result.Items.Select(m =>
                $"{m.MovementDate:yyyy-MM-dd HH:mm} | {m.MovementType} | {m.Ingredient?.Name} | {m.Quantity} | {m.StockBefore}->{m.StockAfter} | {m.ReferenceNumber}"));
            return File(CreateSimplePdf(text), "application/pdf", "inventory-movements.pdf");
        }

        [HttpPost]
        public async Task<IActionResult> CreateInventoryCount([FromBody] CreateInventoryCountRequest request)
        {
            if (request == null || request.BranchId <= 0 || request.Items == null || !request.Items.Any())
            {
                return BadRequest("Invalid inventory count data.");
            }

            try
            {
                var items = request.Items.Select(i => new InventoryCountItemDto
                {
                    IngredientId = i.IngredientId,
                    ActualQuantity = i.ActualQuantity,
                    Reason = i.Reason
                }).ToList();
                var count = await _inventoryService.CreateInventoryCountAsync(request.BranchId, items, request.RequiresApproval, request.Notes, User.Identity?.Name ?? "Admin");
                if (!request.RequiresApproval)
                {
                    await _inventoryService.ApplyInventoryCountAsync(count.Id, User.Identity?.Name ?? "Admin");
                }
                return Ok(new { success = true, id = count.Id, count.CountNumber, count.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveInventoryCount(int id)
        {
            try
            {
                var count = await _inventoryService.ApproveInventoryCountAsync(id, User.Identity?.Name ?? "Admin");
                return Ok(new { success = true, count.Id, count.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApplyInventoryCount(int id)
        {
            try
            {
                var count = await _inventoryService.ApplyInventoryCountAsync(id, User.Identity?.Name ?? "Admin");
                return Ok(new { success = true, count.Id, count.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> RecordWaste([FromBody] RecordWasteRequest request)
        {
            if (request == null || request.BranchId <= 0 || request.IngredientId <= 0 || request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest("Invalid waste record data.");
            }

            try
            {
                var waste = await _inventoryService.RecordWasteAsync(request.BranchId, request.IngredientId, request.Quantity, request.Reason, request.Notes, User.Identity?.Name ?? "Admin");
                return Ok(new { success = true, waste.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetWasteReport(int? branchId, int? ingredientId, DateTime? startDate, DateTime? endDate, string? employee)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            var query = _context.WasteLogs
                .Include(w => w.Branch)
                .Include(w => w.Ingredient)
                .AsQueryable();

            if (activeBranchId.HasValue) query = query.Where(w => w.BranchId == activeBranchId.Value);
            if (ingredientId.HasValue) query = query.Where(w => w.IngredientId == ingredientId.Value);
            if (startDate.HasValue) query = query.Where(w => w.WasteDate >= startDate.Value.Date);
            if (endDate.HasValue) query = query.Where(w => w.WasteDate < endDate.Value.Date.AddDays(1));

            var rows = await query.OrderByDescending(w => w.WasteDate).Take(500).ToListAsync();
            return Json(new {
                totalLoss = rows.Sum(w => w.Cost),
                items = rows.Select(w => new {
                    w.Id,
                    IngredientName = w.Ingredient?.Name,
                    Unit = w.Ingredient?.Unit,
                    BranchName = w.Branch?.Name,
                    w.QuantityWasted,
                    w.Cost,
                    w.Reason,
                    WasteDate = w.WasteDate.ToString("yyyy-MM-dd HH:mm")
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetInventoryAnalytics(int? branchId, DateTime? startDate, DateTime? endDate, string? search)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            var ingredients = _context.Ingredients.Include(i => i.Branch).AsQueryable();
            var movements = _context.InventoryMovements.Include(m => m.Ingredient).AsQueryable();
            var purchases = _context.PurchaseOrders.Include(p => p.Supplier).AsQueryable();
            var waste = _context.WasteLogs.Include(w => w.Ingredient).AsQueryable();

            if (activeBranchId.HasValue)
            {
                ingredients = ingredients.Where(i => i.BranchId == activeBranchId.Value);
                movements = movements.Where(m => m.BranchId == activeBranchId.Value);
                purchases = purchases.Where(p => p.BranchId == activeBranchId.Value);
                waste = waste.Where(w => w.BranchId == activeBranchId.Value);
            }
            if (startDate.HasValue)
            {
                movements = movements.Where(m => m.MovementDate >= startDate.Value.Date);
                purchases = purchases.Where(p => p.OrderDate >= startDate.Value.Date);
                waste = waste.Where(w => w.WasteDate >= startDate.Value.Date);
            }
            if (endDate.HasValue)
            {
                movements = movements.Where(m => m.MovementDate < endDate.Value.Date.AddDays(1));
                purchases = purchases.Where(p => p.OrderDate < endDate.Value.Date.AddDays(1));
                waste = waste.Where(w => w.WasteDate < endDate.Value.Date.AddDays(1));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                ingredients = ingredients.Where(i => i.Name.Contains(search));
            }

            var stockRows = await ingredients.ToListAsync();
            var lowStock = stockRows.Where(i => i.CurrentStock <= i.ReorderLevel).ToList();
            var movementRows = await movements.ToListAsync();
            var purchaseRows = await purchases.ToListAsync();
            var wasteRows = await waste.ToListAsync();

            return Json(new {
                totalInventoryValuation = stockRows.Sum(i => (decimal)i.CurrentStock * i.CostPerUnit),
                lowStockCount = lowStock.Count,
                totalPurchaseAmount = purchaseRows.Sum(p => p.TotalAmount),
                totalWasteAmount = wasteRows.Sum(w => w.Cost),
                movementCount = movementRows.Count,
                consumptionCost = movementRows.Where(m => m.MovementType == "RecipeConsumption").Sum(m => m.TotalCost ?? 0m),
                kpis = new {
                    averageUnitCost = stockRows.Any() ? stockRows.Average(i => i.CostPerUnit) : 0m,
                    stockoutItems = stockRows.Count(i => i.CurrentStock <= 0),
                    receivedOrders = purchaseRows.Count(p => p.Status == "Received")
                },
                lowStock = lowStock.Select(i => new { i.Id, i.Name, i.CurrentStock, i.ReorderLevel, i.Unit }),
                purchaseHistory = purchaseRows.OrderByDescending(p => p.OrderDate).Take(10).Select(p => new { p.OrderNumber, SupplierName = p.Supplier?.Name, p.Status, p.TotalAmount, Date = p.OrderDate.ToString("yyyy-MM-dd") }),
                wasteAnalysis = wasteRows.GroupBy(w => w.Reason).Select(g => new { reason = g.Key, quantity = g.Sum(w => w.QuantityWasted), loss = g.Sum(w => w.Cost) }),
                movementSummary = movementRows.GroupBy(m => m.MovementType).Select(g => new { type = g.Key, count = g.Count(), quantity = g.Sum(m => Math.Abs(m.Quantity)) })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetProductPrices(int menuItemId)
        {
            var prices = await _context.MenuItemPrices
                .Include(p => p.Branch)
                .Where(p => p.MenuItemId == menuItemId && !p.IsDeleted)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.PriceType)
                .ToListAsync();
            return Json(prices.Select(p => new {
                p.Id,
                p.MenuItemId,
                p.BranchId,
                BranchName = p.Branch?.Name,
                p.PriceType,
                p.PriceListName,
                p.Price,
                p.MinQuantity,
                p.CustomerKey,
                StartsOn = p.StartsOn?.ToString("yyyy-MM-dd"),
                EndsOn = p.EndsOn?.ToString("yyyy-MM-dd"),
                p.IsActive,
                p.AllowOverride,
                p.Priority
            }));
        }

        [HttpPost]
        public async Task<IActionResult> SaveProductPrice([FromBody] SaveProductPriceRequest request)
        {
            if (request == null || request.MenuItemId <= 0 || string.IsNullOrWhiteSpace(request.PriceType) || request.Price <= 0)
            {
                return BadRequest("Invalid product price data.");
            }

            var price = request.Id > 0
                ? await _context.MenuItemPrices.FindAsync(request.Id)
                : new MenuItemPrice { CreatedOn = DateTime.Now };

            if (price == null) return NotFound("Product price not found.");

            price.MenuItemId = request.MenuItemId;
            price.BranchId = request.BranchId;
            price.PriceType = request.PriceType;
            price.PriceListName = request.PriceListName;
            price.Price = request.Price;
            price.MinQuantity = request.MinQuantity;
            price.CustomerKey = request.CustomerKey;
            price.StartsOn = request.StartsOn;
            price.EndsOn = request.EndsOn;
            price.IsActive = request.IsActive;
            price.AllowOverride = request.AllowOverride;
            price.Priority = request.Priority;
            price.LastUpdatedOn = DateTime.Now;

            if (request.Id <= 0) _context.MenuItemPrices.Add(price);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, price.Id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProductPrice(int id)
        {
            var price = await _context.MenuItemPrices.FindAsync(id);
            if (price == null)
            {
                return NotFound("Product price not found.");
            }

            price.IsDeleted = true;
            price.IsActive = false;
            price.LastUpdatedOn = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> CreateIngredient([FromBody] CreateIngredientRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.Unit))
            {
                return BadRequest("Invalid ingredient data.");
            }

            var activeBranchId = GetActiveBranchId(request.BranchId);
            if (!activeBranchId.HasValue)
            {
                return BadRequest("Branch ID is required.");
            }

            var ingredient = new Ingredient
            {
                BranchId = activeBranchId.Value,
                Name = request.Name,
                CurrentStock = request.InitialStock,
                ReorderLevel = request.ReorderLevel,
                Unit = request.Unit,
                CostPerUnit = request.CostPerUnit,
                SupplierId = request.SupplierId
            };

            _context.Ingredients.Add(ingredient);
            await _context.SaveChangesAsync();

            return Ok(ingredient);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateIngredient([FromBody] UpdateIngredientRequest request)
        {
            if (request == null || request.Id <= 0 || string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.Unit))
            {
                return BadRequest("Invalid ingredient data.");
            }

            var ingredient = await _context.Ingredients.FindAsync(request.Id);
            if (ingredient == null)
            {
                return NotFound("Ingredient not found.");
            }

            ingredient.Name = request.Name;
            ingredient.ReorderLevel = request.ReorderLevel;
            ingredient.Unit = request.Unit;
            ingredient.CostPerUnit = request.CostPerUnit;
            ingredient.SupplierId = request.SupplierId;

            await _context.SaveChangesAsync();

            return Ok(ingredient);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteIngredient(int id)
        {
            var ingredient = await _context.Ingredients.FindAsync(id);
            if (ingredient == null)
            {
                return NotFound("Ingredient not found.");
            }

            var recipes = await _context.Recipes.Where(r => r.IngredientId == id).ToListAsync();
            if (recipes.Any())
            {
                _context.Recipes.RemoveRange(recipes);
            }

            var adjustments = await _context.InventoryAdjustments.Where(a => a.IngredientId == id).ToListAsync();
            if (adjustments.Any())
            {
                _context.InventoryAdjustments.RemoveRange(adjustments);
            }

            var waste = await _context.WasteLogs.Where(w => w.IngredientId == id).ToListAsync();
            if (waste.Any())
            {
                _context.WasteLogs.RemoveRange(waste);
            }

            _context.Ingredients.Remove(ingredient);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetDailyConsumptionReport(string? date, int? branchId)
        {
            var activeBranchId = GetActiveBranchId(branchId);
            if (!activeBranchId.HasValue)
            {
                return BadRequest("No active branch selected.");
            }

            DateTime selectedDate;
            if (!DateTime.TryParse(date, out selectedDate))
            {
                selectedDate = DateTime.Today;
            }

            // 1. Get all completed/paid orders for this branch and date
            var completedOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.BranchId == activeBranchId.Value && 
                            (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid) &&
                            o.OrderDate.Date == selectedDate.Date)
                .ToListAsync();

            // 2. Aggregate sold menu items
            var soldItems = completedOrders
                .SelectMany(o => o.OrderItems)
                .GroupBy(oi => new { oi.MenuItemId, Name = oi.MenuItem != null ? oi.MenuItem.Name : $"Product {oi.MenuItemId}", Price = oi.MenuItem != null ? oi.MenuItem.Price : 0 })
                .Select(g => new {
                    MenuItemId = g.Key.MenuItemId,
                    Name = g.Key.Name,
                    Price = g.Key.Price,
                    QuantitySold = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.Quantity * (double)g.Key.Price)
                })
                .ToList();

            // 3. Get all ingredients for this branch to display current stock levels
            var branchIngredients = await _context.Ingredients
                .Where(i => i.BranchId == activeBranchId.Value)
                .ToListAsync();

            // 4. Get all recipes for this branch's menu items
            var menuItemIds = soldItems.Select(s => s.MenuItemId).ToList();
            var recipes = await _context.Recipes
                .Include(r => r.Ingredient)
                .Where(r => menuItemIds.Contains(r.MenuItemId))
                .ToListAsync();

            // 5. Calculate consumption per ingredient
            var consumptionSummary = new List<object>();

            foreach (var ingredient in branchIngredients)
            {
                double theoreticalConsumption = 0;
                
                // Find all recipe portions mapping to this ingredient by name match (cross-branch compliant)
                var matchingRecipes = recipes
                    .Where(r => r.Ingredient != null && r.Ingredient.Name.Trim().ToLower() == ingredient.Name.Trim().ToLower())
                    .ToList();

                foreach (var recipe in matchingRecipes)
                {
                    var soldItem = soldItems.FirstOrDefault(s => s.MenuItemId == recipe.MenuItemId);
                    if (soldItem != null)
                    {
                        theoreticalConsumption += recipe.QuantityRequired * soldItem.QuantitySold;
                    }
                }

                // Sum actual adjustments under "OrderConsumption" for this date
                var actualDeductions = await _context.InventoryAdjustments
                    .Where(a => a.BranchId == activeBranchId.Value && 
                                a.IngredientId == ingredient.Id && 
                                a.Type == "OrderConsumption" && 
                                a.AdjustmentDate.Date == selectedDate.Date)
                    .SumAsync(a => a.QuantityAdjusted);

                double actualDeducted = Math.Abs(actualDeductions);

                if (theoreticalConsumption > 0 || actualDeducted > 0)
                {
                    string portionReference = "";
                    if (matchingRecipes.Any())
                    {
                        var firstRecipe = matchingRecipes.First();
                        var menuItemName = _context.MenuItems.Find(firstRecipe.MenuItemId)?.Name ?? "Item";
                        portionReference = $"{firstRecipe.QuantityRequired} {ingredient.Unit} per {menuItemName}";
                    }

                    consumptionSummary.Add(new {
                        IngredientId = ingredient.Id,
                        Name = ingredient.Name,
                        Unit = ingredient.Unit,
                        CurrentStock = ingredient.CurrentStock,
                        TheoreticalConsumption = theoreticalConsumption,
                        ActualDeducted = actualDeducted,
                        PortionReference = portionReference,
                        Variance = theoreticalConsumption - actualDeducted
                    });
                }
            }

            // 6. Get all waste adjustments for this branch and date
            var dailyWaste = await _context.InventoryAdjustments
                .Include(a => a.Ingredient)
                .Where(a => a.BranchId == activeBranchId.Value && 
                            a.AdjustmentDate.Date == selectedDate.Date &&
                            (a.Type == "Waste" || a.Type == "Expired" || a.Type == "Damage" || a.Type == "Theft"))
                .OrderByDescending(a => a.AdjustmentDate)
                .Select(a => new {
                    a.Id,
                    IngredientName = a.Ingredient != null ? a.Ingredient.Name : "Unknown",
                    Quantity = Math.Abs(a.QuantityAdjusted),
                    Unit = a.Ingredient != null ? a.Ingredient.Unit : "Kg",
                    Cost = (decimal)Math.Abs(a.QuantityAdjusted) * (a.Ingredient != null ? a.Ingredient.CostPerUnit : 0m),
                    Type = a.Type,
                    Notes = a.Notes,
                    Time = a.AdjustmentDate.ToString("HH:mm"),
                    AdjustedBy = a.AdjustedBy
                })
                .ToListAsync();

            return Json(new {
                Date = selectedDate.ToString("yyyy-MM-dd"),
                SoldItems = soldItems,
                Consumption = consumptionSummary,
                Waste = dailyWaste
            });
        }

        // --- Helper Methods ---

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static byte[] CreateSimplePdf(string text)
        {
            var safeText = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            var lines = safeText.Split('\n').Take(45).ToList();
            var content = new StringBuilder();
            content.AppendLine("BT /F1 10 Tf 40 780 Td");
            foreach (var line in lines)
            {
                content.Append("(").Append(line).AppendLine(") Tj 0 -14 Td");
            }
            content.AppendLine("ET");

            var stream = content.ToString();
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream"
            };

            var pdf = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int> { 0 };
            foreach (var obj in objects)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
                pdf.Append(offsets.Count - 1).Append(" 0 obj\n").Append(obj).Append("\nendobj\n");
            }
            var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
            pdf.Append("xref\n0 ").Append(objects.Count + 1).Append("\n0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1))
            {
                pdf.Append(offset.ToString("0000000000")).Append(" 00000 n \n");
            }
            pdf.Append("trailer << /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF");
            return Encoding.ASCII.GetBytes(pdf.ToString());
        }

        private int? GetActiveBranchId(int? queryBranchId)
        {
            if (queryBranchId.HasValue) return queryBranchId.Value;

            // Check Cookie context
            var cookieBranch = Request.Cookies["AdminActiveBranchId"];
            if (!string.IsNullOrEmpty(cookieBranch) && int.TryParse(cookieBranch, out int activeBranch))
            {
                return activeBranch;
            }

            // Check User claims
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
                if (user != null && user.BranchId.HasValue)
                {
                    return user.BranchId.Value;
                }
            }

            return null;
        }
    }

    // --- Request Model Classes ---

    public class AddStockRequest
    {
        public int BranchId { get; set; }
        public int IngredientId { get; set; }
        public double Quantity { get; set; }
        public decimal? Cost { get; set; }
        public string? Notes { get; set; }
    }

    public class AdjustStockRequest
    {
        public int BranchId { get; set; }
        public int IngredientId { get; set; }
        public double QuantityAdjusted { get; set; }
        public string Type { get; set; }
        public string? Notes { get; set; }
    }

    public class RequestTransferRequest
    {
        public int SourceBranchId { get; set; }
        public int DestinationBranchId { get; set; }
        public List<TransferItemRequest> Items { get; set; }
        public string? Notes { get; set; }
    }

    public class TransferItemRequest
    {
        public string IngredientName { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
    }

    public class SaveRecipeRequest
    {
        public int MenuItemId { get; set; }
        public List<RecipeItemRequest> Items { get; set; }
    }

    public class RecipeItemRequest
    {
        public string IngredientName { get; set; }
        public double QuantityRequired { get; set; }
        public string Unit { get; set; }
    }

    public class CreateIngredientRequest
    {
        public int? BranchId { get; set; }
        public string Name { get; set; }
        public double InitialStock { get; set; }
        public double ReorderLevel { get; set; }
        public string Unit { get; set; }
        public decimal CostPerUnit { get; set; }
        public int? SupplierId { get; set; }
    }

    public class UpdateIngredientRequest
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double ReorderLevel { get; set; }
        public string Unit { get; set; }
        public decimal CostPerUnit { get; set; }
        public int? SupplierId { get; set; }
    }

    public class SaveSupplierRequest
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public double LeadTimeDays { get; set; } = 2.5;
        public double QualityRating { get; set; } = 4.5;
    }

    public class CreatePurchaseOrderRequest
    {
        public int BranchId { get; set; }
        public int SupplierId { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string? Notes { get; set; }
        public List<CreatePurchaseOrderItemRequest> Items { get; set; } = new List<CreatePurchaseOrderItemRequest>();
    }

    public class CreatePurchaseOrderItemRequest
    {
        public int IngredientId { get; set; }
        public double Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class ReceivePurchaseOrderRequest
    {
        public int Id { get; set; }
        public List<ReceivePurchaseOrderItemRequest>? Items { get; set; }
    }

    public class ReceivePurchaseOrderItemRequest
    {
        public int PurchaseOrderItemId { get; set; }
        public double QuantityReceived { get; set; }
    }

    public class CreateInventoryCountRequest
    {
        public int BranchId { get; set; }
        public bool RequiresApproval { get; set; }
        public string? Notes { get; set; }
        public List<CreateInventoryCountItemRequest> Items { get; set; } = new List<CreateInventoryCountItemRequest>();
    }

    public class CreateInventoryCountItemRequest
    {
        public int IngredientId { get; set; }
        public double ActualQuantity { get; set; }
        public string Reason { get; set; }
    }

    public class RecordWasteRequest
    {
        public int BranchId { get; set; }
        public int IngredientId { get; set; }
        public double Quantity { get; set; }
        public string Reason { get; set; }
        public string? Notes { get; set; }
    }

    public class SaveProductPriceRequest
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public int? BranchId { get; set; }
        public string PriceType { get; set; }
        public string? PriceListName { get; set; }
        public decimal Price { get; set; }
        public int? MinQuantity { get; set; }
        public string? CustomerKey { get; set; }
        public DateTime? StartsOn { get; set; }
        public DateTime? EndsOn { get; set; }
        public bool IsActive { get; set; } = true;
        public bool AllowOverride { get; set; } = true;
        public int Priority { get; set; } = 100;
    }
}
