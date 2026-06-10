using Microsoft.EntityFrameworkCore;
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using Resturant.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Resturant.Services.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _context;

        public InventoryService(AppDbContext context)
        {
            _context = context;
        }

        // --- Ingredients & Stock Levels ---

        public async Task<IEnumerable<Ingredient>> GetStockLevelsAsync(int? branchId)
        {
            var query = _context.Ingredients
                .Include(i => i.Supplier)
                .Include(i => i.Branch)
                .AsQueryable();

            if (branchId.HasValue)
            {
                query = query.Where(i => i.BranchId == branchId.Value);
            }

            return await query.OrderBy(i => i.Name).ToListAsync();
        }

        public async Task<Ingredient> AddStockAsync(int branchId, int ingredientId, double quantity, decimal? cost, string? notes, string? user)
        {
            var ingredient = await _context.Ingredients
                .FirstOrDefaultAsync(i => i.Id == ingredientId && i.BranchId == branchId);

            if (ingredient == null)
            {
                throw new KeyNotFoundException($"Ingredient with ID {ingredientId} not found in branch {branchId}.");
            }

            ingredient.CurrentStock += quantity;
            if (cost.HasValue && cost.Value > 0)
            {
                ingredient.CostPerUnit = cost.Value;
            }

            var adjustment = new InventoryAdjustment
            {
                BranchId = branchId,
                IngredientId = ingredientId,
                QuantityAdjusted = quantity,
                Type = "Purchase",
                Notes = notes ?? $"Stock purchase of {quantity} {ingredient.Unit}",
                AdjustmentDate = DateTime.Now,
                AdjustedBy = user ?? "System"
            };

            _context.InventoryAdjustments.Add(adjustment);
            _context.Ingredients.Update(ingredient);
            await _context.SaveChangesAsync();

            return ingredient;
        }

        public async Task<Ingredient> AdjustStockAsync(int branchId, int ingredientId, double quantityAdjusted, string type, string? notes, string? user)
        {
            var ingredient = await _context.Ingredients
                .FirstOrDefaultAsync(i => i.Id == ingredientId && i.BranchId == branchId);

            if (ingredient == null)
            {
                throw new KeyNotFoundException($"Ingredient with ID {ingredientId} not found in branch {branchId}.");
            }

            // Automatically treat positive inputs as deductions (negative adjustments) for waste and loss types
            if (type.Equals("Waste", StringComparison.OrdinalIgnoreCase) || 
                type.Equals("Expired", StringComparison.OrdinalIgnoreCase) || 
                type.Equals("Damage", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("Theft", StringComparison.OrdinalIgnoreCase))
            {
                if (quantityAdjusted > 0)
                {
                    quantityAdjusted = -quantityAdjusted;
                }
            }

            ingredient.CurrentStock += quantityAdjusted;
            if (ingredient.CurrentStock < 0) ingredient.CurrentStock = 0; // Prevent negative stock

            // Log generic inventory adjustment
            var adjustment = new InventoryAdjustment
            {
                BranchId = branchId,
                IngredientId = ingredientId,
                QuantityAdjusted = quantityAdjusted,
                Type = type,
                Notes = notes ?? $"Manual stock adjustment: {quantityAdjusted} {ingredient.Unit}",
                AdjustmentDate = DateTime.Now,
                AdjustedBy = user ?? "System"
            };
            _context.InventoryAdjustments.Add(adjustment);

            // Special handling: if type is Waste, Spoilage, Damage, etc., also log in legacy WasteLog
            if (quantityAdjusted < 0 && (type.Equals("Waste", StringComparison.OrdinalIgnoreCase) || 
                                          type.Equals("Expired", StringComparison.OrdinalIgnoreCase) || 
                                          type.Equals("Damage", StringComparison.OrdinalIgnoreCase)))
            {
                var wasteQty = Math.Abs(quantityAdjusted);
                var waste = new WasteLog
                {
                    BranchId = branchId,
                    IngredientId = ingredientId,
                    QuantityWasted = wasteQty,
                    Cost = (decimal)wasteQty * ingredient.CostPerUnit,
                    Reason = type,
                    WasteDate = DateTime.Now
                };
                _context.WasteLogs.Add(waste);

                // Also log as an expense under "Waste" category
                var expense = new Expense
                {
                    BranchId = branchId,
                    Title = $"Waste Loss: {ingredient.Name}",
                    Description = $"{wasteQty} {ingredient.Unit} wasted/expired. Reason: {type}. {notes}",
                    Amount = waste.Cost,
                    Category = "Waste",
                    ExpenseDate = DateTime.Now,
                    MerchantName = "Internal Waste",
                    PaymentMethod = "N/A"
                };
                _context.Expenses.Add(expense);
            }

            _context.Ingredients.Update(ingredient);
            await _context.SaveChangesAsync();

            return ingredient;
        }

        // --- Automated Recipe Consumption ---

        public async Task DeductStockForOrderAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return;

            foreach (var item in order.OrderItems)
            {
                // Find recipes for this MenuItem
                var recipes = await _context.Recipes
                    .Include(r => r.Ingredient)
                    .Where(r => r.MenuItemId == item.MenuItemId)
                    .ToListAsync();

                foreach (var recipe in recipes)
                {
                    // Find the ingredient in the order's specific branch (by matching name)
                    var branchIngredient = await _context.Ingredients
                        .FirstOrDefaultAsync(i => i.BranchId == order.BranchId && i.Name == recipe.Ingredient.Name);

                    if (branchIngredient != null)
                    {
                        double quantityNeeded = recipe.QuantityRequired * item.Quantity;
                        branchIngredient.CurrentStock -= quantityNeeded;
                        if (branchIngredient.CurrentStock < 0) branchIngredient.CurrentStock = 0;

                        // Log adjustment
                        var adjustment = new InventoryAdjustment
                        {
                            BranchId = order.BranchId,
                            IngredientId = branchIngredient.Id,
                            QuantityAdjusted = -quantityNeeded,
                            Type = "OrderConsumption",
                            Notes = $"Automatic deduction for Order #{order.Id}, Item: {item.MenuItem?.Name}",
                            AdjustmentDate = DateTime.Now,
                            AdjustedBy = "Order System"
                        };
                        _context.InventoryAdjustments.Add(adjustment);
                        _context.Ingredients.Update(branchIngredient);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeductStockForShiftAsync(int shiftId)
        {
            var shift = await _context.Set<CashierShift>()
                .FirstOrDefaultAsync(s => s.Id == shiftId);

            if (shift == null) return;

            // Get all completed/paid orders for this shift
            var shiftOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.ShiftId == shiftId && 
                            (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid) &&
                            o.BranchId == shift.BranchId)
                .ToListAsync();

            if (!shiftOrders.Any()) return;

            // Group all order items by MenuItemId and sum quantities
            var orderItemsGrouped = shiftOrders
                .SelectMany(o => o.OrderItems)
                .Where(oi => !oi.IsCancelled)
                .GroupBy(oi => oi.MenuItemId)
                .Select(g => new {
                    MenuItemId = g.Key,
                    TotalQuantity = g.Sum(oi => oi.Quantity)
                })
                .ToList();

            var menuItemIds = orderItemsGrouped.Select(g => g.MenuItemId).ToList();

            // Fetch recipes for all sold items
            var recipes = await _context.Recipes
                .Include(r => r.Ingredient)
                .Where(r => menuItemIds.Contains(r.MenuItemId))
                .ToListAsync();

            // Fetch all local branch ingredients to update
            var branchIngredients = await _context.Ingredients
                .Where(i => i.BranchId == shift.BranchId)
                .ToListAsync();

            var ingredientConsumption = new Dictionary<string, double>();

            foreach (var groupedItem in orderItemsGrouped)
            {
                var itemRecipes = recipes.Where(r => r.MenuItemId == groupedItem.MenuItemId).ToList();
                foreach (var recipe in itemRecipes)
                {
                    if (recipe.Ingredient != null)
                    {
                        var ingNameKey = recipe.Ingredient.Name.Trim().ToLower();
                        double quantityNeeded = recipe.QuantityRequired * groupedItem.TotalQuantity;

                        if (ingredientConsumption.ContainsKey(ingNameKey))
                        {
                            ingredientConsumption[ingNameKey] += quantityNeeded;
                        }
                        else
                        {
                            ingredientConsumption[ingNameKey] = quantityNeeded;
                        }
                    }
                }
            }

            foreach (var cons in ingredientConsumption)
            {
                var branchIngredient = branchIngredients
                    .FirstOrDefault(i => i.Name.Trim().ToLower() == cons.Key);

                if (branchIngredient != null && cons.Value > 0)
                {
                    branchIngredient.CurrentStock -= cons.Value;
                    if (branchIngredient.CurrentStock < 0) branchIngredient.CurrentStock = 0;

                    // Log combined adjustment for the shift
                    var adjustment = new InventoryAdjustment
                    {
                        BranchId = shift.BranchId,
                        IngredientId = branchIngredient.Id,
                        QuantityAdjusted = -cons.Value,
                        Type = "OrderConsumption",
                        Notes = $"Automatic consumption deduction on Shift #{shiftId} closure",
                        AdjustmentDate = DateTime.Now,
                        AdjustedBy = "Shift Closing System"
                    };
                    _context.InventoryAdjustments.Add(adjustment);
                    _context.Ingredients.Update(branchIngredient);
                }
            }

            await _context.SaveChangesAsync();
        }

        // --- Stock Transfers Workflow ---

        public async Task<IEnumerable<StockTransfer>> GetTransfersAsync(int? branchId)
        {
            var query = _context.StockTransfers
                .Include(t => t.SourceBranch)
                .Include(t => t.DestinationBranch)
                .Include(t => t.Items)
                .AsQueryable();

            if (branchId.HasValue)
            {
                query = query.Where(t => t.SourceBranchId == branchId.Value || t.DestinationBranchId == branchId.Value);
            }

            return await query.OrderByDescending(t => t.CreatedOn).ToListAsync();
        }

        public async Task<StockTransfer> RequestTransferAsync(int sourceBranchId, int destinationBranchId, List<StockTransferItemDto> items, string? notes, string? user)
        {
            if (sourceBranchId == destinationBranchId)
            {
                throw new ArgumentException("Source and destination branches cannot be the same.");
            }

            var transfer = new StockTransfer
            {
                SourceBranchId = sourceBranchId,
                DestinationBranchId = destinationBranchId,
                Status = "Pending",
                Notes = notes,
                CreatedBy = user ?? "System"
            };

            _context.StockTransfers.Add(transfer);
            await _context.SaveChangesAsync(); // Get transfer ID

            foreach (var item in items)
            {
                var transferItem = new StockTransferItem
                {
                    StockTransferId = transfer.Id,
                    IngredientName = item.IngredientName,
                    Quantity = item.Quantity,
                    Unit = item.Unit
                };
                _context.StockTransferItems.Add(transferItem);
            }

            await _context.SaveChangesAsync();
            return transfer;
        }

        public async Task<StockTransfer> ApproveTransferAsync(int transferId, string? user)
        {
            var transfer = await _context.StockTransfers
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == transferId);

            if (transfer == null)
            {
                throw new KeyNotFoundException($"Stock transfer with ID {transferId} not found.");
            }

            if (transfer.Status != "Pending")
            {
                throw new InvalidOperationException($"Cannot approve a transfer that is in '{transfer.Status}' status.");
            }

            // Deduct stock from Source Branch
            foreach (var item in transfer.Items)
            {
                var sourceIngredient = await _context.Ingredients
                    .FirstOrDefaultAsync(i => i.BranchId == transfer.SourceBranchId && i.Name == item.IngredientName);

                if (sourceIngredient == null)
                {
                    var templateIngredient = await _context.Ingredients
                        .FirstOrDefaultAsync(i => i.Name == item.IngredientName);

                    sourceIngredient = new Ingredient
                    {
                        BranchId = transfer.SourceBranchId,
                        Name = item.IngredientName,
                        CurrentStock = 0,
                        ReorderLevel = templateIngredient?.ReorderLevel ?? 5.0,
                        Unit = item.Unit,
                        CostPerUnit = templateIngredient?.CostPerUnit ?? 0m,
                        SupplierId = templateIngredient?.SupplierId
                    };
                    _context.Ingredients.Add(sourceIngredient);
                    await _context.SaveChangesAsync();
                }

                if (sourceIngredient.CurrentStock < item.Quantity)
                {
                    // Allow negative/forced approval but log/warn
                }

                sourceIngredient.CurrentStock -= item.Quantity;
                if (sourceIngredient.CurrentStock < 0) sourceIngredient.CurrentStock = 0;

                var adjustment = new InventoryAdjustment
                {
                    BranchId = transfer.SourceBranchId,
                    IngredientId = sourceIngredient.Id,
                    QuantityAdjusted = -item.Quantity,
                    Type = "Transfer",
                    Notes = $"Stock transfer #{transfer.Id} to branch {transfer.DestinationBranchId}",
                    AdjustmentDate = DateTime.Now,
                    AdjustedBy = user ?? "System"
                };

                _context.InventoryAdjustments.Add(adjustment);
                _context.Ingredients.Update(sourceIngredient);
            }

            transfer.Status = "InTransit";
            transfer.ProcessedBy = user ?? "System";
            transfer.ProcessedDate = DateTime.Now;

            _context.StockTransfers.Update(transfer);
            await _context.SaveChangesAsync();
            return transfer;
        }

        public async Task<StockTransfer> ReceiveTransferAsync(int transferId, string? user)
        {
            var transfer = await _context.StockTransfers
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == transferId);

            if (transfer == null)
            {
                throw new KeyNotFoundException($"Stock transfer with ID {transferId} not found.");
            }

            if (transfer.Status != "InTransit" && transfer.Status != "Approved")
            {
                throw new InvalidOperationException($"Cannot receive a transfer in '{transfer.Status}' status.");
            }

            // Add stock to Destination Branch
            foreach (var item in transfer.Items)
            {
                var destIngredient = await _context.Ingredients
                    .FirstOrDefaultAsync(i => i.BranchId == transfer.DestinationBranchId && i.Name == item.IngredientName);

                // Fetch source ingredient properties as a template if creating a new one
                var sourceIngredient = await _context.Ingredients
                    .FirstOrDefaultAsync(i => i.BranchId == transfer.SourceBranchId && i.Name == item.IngredientName);

                if (destIngredient == null)
                {
                    // Create ingredient in destination branch automatically
                    destIngredient = new Ingredient
                    {
                        BranchId = transfer.DestinationBranchId,
                        Name = item.IngredientName,
                        CurrentStock = item.Quantity,
                        ReorderLevel = sourceIngredient?.ReorderLevel ?? 5.0,
                        Unit = item.Unit,
                        CostPerUnit = sourceIngredient?.CostPerUnit ?? 0m,
                        SupplierId = sourceIngredient?.SupplierId
                    };
                    _context.Ingredients.Add(destIngredient);
                    await _context.SaveChangesAsync(); // Generate ID for adjustment
                }
                else
                {
                    destIngredient.CurrentStock += item.Quantity;
                    _context.Ingredients.Update(destIngredient);
                }

                var adjustment = new InventoryAdjustment
                {
                    BranchId = transfer.DestinationBranchId,
                    IngredientId = destIngredient.Id,
                    QuantityAdjusted = item.Quantity,
                    Type = "Transfer",
                    Notes = $"Received stock transfer #{transfer.Id} from branch {transfer.SourceBranchId}",
                    AdjustmentDate = DateTime.Now,
                    AdjustedBy = user ?? "System"
                };

                _context.InventoryAdjustments.Add(adjustment);
            }

            transfer.Status = "Received";
            transfer.ReceivedBy = user ?? "System";
            transfer.ReceivedDate = DateTime.Now;

            _context.StockTransfers.Update(transfer);
            await _context.SaveChangesAsync();
            return transfer;
        }

        public async Task<StockTransfer> CancelTransferAsync(int transferId, string? user)
        {
            var transfer = await _context.StockTransfers
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == transferId);

            if (transfer == null)
            {
                throw new KeyNotFoundException($"Stock transfer with ID {transferId} not found.");
            }

            if (transfer.Status == "Received" || transfer.Status == "Cancelled")
            {
                throw new InvalidOperationException($"Cannot cancel a transfer that is already '{transfer.Status}'.");
            }

            // Return stock to Source Branch if it was already approved and in transit
            if (transfer.Status == "InTransit")
            {
                foreach (var item in transfer.Items)
                {
                    var sourceIngredient = await _context.Ingredients
                        .FirstOrDefaultAsync(i => i.BranchId == transfer.SourceBranchId && i.Name == item.IngredientName);

                    if (sourceIngredient != null)
                    {
                        sourceIngredient.CurrentStock += item.Quantity;

                        var adjustment = new InventoryAdjustment
                        {
                            BranchId = transfer.SourceBranchId,
                            IngredientId = sourceIngredient.Id,
                            QuantityAdjusted = item.Quantity,
                            Type = "TransferCancel",
                            Notes = $"Cancelled transfer #{transfer.Id}. Stock returned.",
                            AdjustmentDate = DateTime.Now,
                            AdjustedBy = user ?? "System"
                        };

                        _context.InventoryAdjustments.Add(adjustment);
                        _context.Ingredients.Update(sourceIngredient);
                    }
                }
            }

            transfer.Status = "Cancelled";
            transfer.ProcessedBy = user ?? "System";
            transfer.ProcessedDate = DateTime.Now;

            _context.StockTransfers.Update(transfer);
            await _context.SaveChangesAsync();
            return transfer;
        }

        // --- Recipe Management ---

        public async Task<IEnumerable<Recipe>> GetRecipesForMenuItemAsync(int menuItemId)
        {
            return await _context.Recipes
                .Include(r => r.Ingredient)
                .Where(r => r.MenuItemId == menuItemId)
                .ToListAsync();
        }

        public async Task SaveRecipeAsync(int menuItemId, List<RecipeItemDto> items)
        {
            var menuItem = await _context.MenuItems.FindAsync(menuItemId);
            if (menuItem == null)
            {
                throw new KeyNotFoundException($"Menu Item with ID {menuItemId} not found.");
            }

            // Clear existing recipes
            var existingRecipes = await _context.Recipes.Where(r => r.MenuItemId == menuItemId).ToListAsync();
            _context.Recipes.RemoveRange(existingRecipes);
            await _context.SaveChangesAsync();

            foreach (var item in items)
            {
                // Find or create ingredient in the menu item's branch
                var ingredient = await _context.Ingredients
                    .FirstOrDefaultAsync(i => i.BranchId == menuItem.BranchId && i.Name == item.IngredientName);

                if (ingredient == null)
                {
                    // Create ingredient automatically to ensure easy setup
                    ingredient = new Ingredient
                    {
                        BranchId = menuItem.BranchId,
                        Name = item.IngredientName,
                        CurrentStock = 0,
                        ReorderLevel = 5.0,
                        Unit = item.Unit ?? "Kg",
                        CostPerUnit = 0m
                    };
                    _context.Ingredients.Add(ingredient);
                    await _context.SaveChangesAsync();
                }

                var recipe = new Recipe
                {
                    MenuItemId = menuItemId,
                    IngredientId = ingredient.Id,
                    QuantityRequired = item.QuantityRequired
                };
                _context.Recipes.Add(recipe);
            }

            await _context.SaveChangesAsync();
        }
    }
}
