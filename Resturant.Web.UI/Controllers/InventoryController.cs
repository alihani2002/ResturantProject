using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using Resturant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Web.UI.Controllers
{
    [Authorize]
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
        
        public IActionResult Index()
        {
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
            var query = _context.Suppliers.AsQueryable();

            if (activeBranchId.HasValue)
            {
                query = query.Where(s => s.BranchId == activeBranchId.Value);
            }

            var suppliers = await query
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name, s.BranchId })
                .ToListAsync();

            return Json(suppliers);
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

            return Json(new {
                Date = selectedDate.ToString("yyyy-MM-dd"),
                SoldItems = soldItems,
                Consumption = consumptionSummary
            });
        }

        // --- Helper Methods ---

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
}
