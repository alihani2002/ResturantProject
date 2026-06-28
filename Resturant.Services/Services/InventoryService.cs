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
            using var transaction = await _context.Database.BeginTransactionAsync();

            var ingredient = await _context.Ingredients
                .FirstOrDefaultAsync(i => i.Id == ingredientId && i.BranchId == branchId);

            if (ingredient == null)
            {
                throw new KeyNotFoundException($"Ingredient with ID {ingredientId} not found in branch {branchId}.");
            }

            var stockBefore = ingredient.CurrentStock;
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
            AddMovement(branchId, ingredient, "Purchase", quantity, stockBefore, ingredient.CurrentStock, cost ?? ingredient.CostPerUnit, null, user, adjustment.Notes);
            _context.Ingredients.Update(ingredient);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ingredient;
        }

        public async Task<Ingredient> AdjustStockAsync(int branchId, int ingredientId, double quantityAdjusted, string type, string? notes, string? user)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

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
                type.Equals("Spoiled", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("Lost", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("CustomerReturn", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("Theft", StringComparison.OrdinalIgnoreCase))
            {
                if (quantityAdjusted > 0)
                {
                    quantityAdjusted = -quantityAdjusted;
                }
            }

            var stockBefore = ingredient.CurrentStock;
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
            var movementType = GetMovementType(type);
            AddMovement(branchId, ingredient, movementType, quantityAdjusted, stockBefore, ingredient.CurrentStock, ingredient.CostPerUnit, null, user, adjustment.Notes);

            // Special handling: if type is Waste, Spoilage, Damage, etc., also log in legacy WasteLog
            if (quantityAdjusted < 0 && (type.Equals("Waste", StringComparison.OrdinalIgnoreCase) || 
                                          type.Equals("Expired", StringComparison.OrdinalIgnoreCase) || 
                                          type.Equals("Damage", StringComparison.OrdinalIgnoreCase) ||
                                          type.Equals("Spoiled", StringComparison.OrdinalIgnoreCase) ||
                                          type.Equals("Lost", StringComparison.OrdinalIgnoreCase) ||
                                          type.Equals("CustomerReturn", StringComparison.OrdinalIgnoreCase)))
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
            await transaction.CommitAsync();

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
                    if (recipe.Ingredient == null) continue;

                    // Find the ingredient in the order's specific branch (by matching name)
                    var branchIngredient = await _context.Ingredients
                        .FirstOrDefaultAsync(i => i.BranchId == order.BranchId && i.Name == recipe.Ingredient.Name);

                    if (branchIngredient != null)
                    {
                        double quantityNeeded = recipe.QuantityRequired * item.Quantity;
                        var stockBefore = branchIngredient.CurrentStock;
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
                        AddMovement(order.BranchId, branchIngredient, "RecipeConsumption", -quantityNeeded, stockBefore, branchIngredient.CurrentStock, branchIngredient.CostPerUnit, $"ORD-{order.Id}", "Order System", adjustment.Notes);
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
                    var stockBefore = branchIngredient.CurrentStock;
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
                    AddMovement(shift.BranchId, branchIngredient, "RecipeConsumption", -cons.Value, stockBefore, branchIngredient.CurrentStock, branchIngredient.CostPerUnit, $"SHIFT-{shiftId}", "Shift Closing System", adjustment.Notes);
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
            using var transaction = await _context.Database.BeginTransactionAsync();

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

                var stockBefore = sourceIngredient.CurrentStock;
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
                AddMovement(transfer.SourceBranchId, sourceIngredient, "TransferOut", -item.Quantity, stockBefore, sourceIngredient.CurrentStock, sourceIngredient.CostPerUnit, $"TR-{transfer.Id}", user, adjustment.Notes);
                _context.Ingredients.Update(sourceIngredient);
            }

            transfer.Status = "InTransit";
            transfer.ProcessedBy = user ?? "System";
            transfer.ProcessedDate = DateTime.Now;

            _context.StockTransfers.Update(transfer);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return transfer;
        }

        public async Task<StockTransfer> ReceiveTransferAsync(int transferId, string? user)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

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

                double stockBefore;
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
                    stockBefore = 0;
                }
                else
                {
                    stockBefore = destIngredient.CurrentStock;
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
                AddMovement(transfer.DestinationBranchId, destIngredient, "TransferIn", item.Quantity, stockBefore, destIngredient.CurrentStock, destIngredient.CostPerUnit, $"TR-{transfer.Id}", user, adjustment.Notes);
            }

            transfer.Status = "Received";
            transfer.ReceivedBy = user ?? "System";
            transfer.ReceivedDate = DateTime.Now;

            _context.StockTransfers.Update(transfer);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return transfer;
        }

        public async Task<StockTransfer> CancelTransferAsync(int transferId, string? user)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

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
                        var stockBefore = sourceIngredient.CurrentStock;
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
                        AddMovement(transfer.SourceBranchId, sourceIngredient, "TransferCancel", item.Quantity, stockBefore, sourceIngredient.CurrentStock, sourceIngredient.CostPerUnit, $"TR-{transfer.Id}", user, adjustment.Notes);
                        _context.Ingredients.Update(sourceIngredient);
                    }
                }
            }

            transfer.Status = "Cancelled";
            transfer.ProcessedBy = user ?? "System";
            transfer.ProcessedDate = DateTime.Now;

            _context.StockTransfers.Update(transfer);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return transfer;
        }

        // --- Purchase Orders, Movement History, Counts and Waste ---

        public async Task<IEnumerable<Supplier>> GetSuppliersAsync(int? branchId)
        {
            var query = _context.Suppliers.Include(s => s.Branch).AsQueryable();
            if (branchId.HasValue)
            {
                query = query.Where(s => s.BranchId == branchId.Value);
            }
            return await query.OrderBy(s => s.Name).ToListAsync();
        }

        public async Task<Supplier> SaveSupplierAsync(Supplier supplier)
        {
            if (supplier.Id > 0)
            {
                var existing = await _context.Suppliers.FindAsync(supplier.Id);
                if (existing == null) throw new KeyNotFoundException("Supplier not found.");

                existing.Name = supplier.Name;
                existing.Phone = supplier.Phone;
                existing.Email = supplier.Email;
                existing.Address = supplier.Address;
                existing.LeadTimeDays = supplier.LeadTimeDays;
                existing.QualityRating = supplier.QualityRating;
                existing.BranchId = supplier.BranchId;
                existing.LastUpdatedOn = DateTime.Now;
            }
            else
            {
                supplier.CreatedOn = DateTime.Now;
                _context.Suppliers.Add(supplier);
            }

            await _context.SaveChangesAsync();
            return supplier;
        }

        public async Task<IEnumerable<PurchaseOrder>> GetPurchaseOrdersAsync(int? branchId, string? status)
        {
            var query = _context.PurchaseOrders
                .Include(p => p.Branch)
                .Include(p => p.Supplier)
                .Include(p => p.Items)
                    .ThenInclude(i => i.Ingredient)
                .AsQueryable();

            if (branchId.HasValue) query = query.Where(p => p.BranchId == branchId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);

            return await query.OrderByDescending(p => p.OrderDate).ToListAsync();
        }

        public async Task<PurchaseOrder> CreatePurchaseOrderAsync(int branchId, int supplierId, List<PurchaseOrderItemDto> items, DateTime? expectedDate, string? notes, string? user)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Purchase order must include at least one item.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            var order = new PurchaseOrder
            {
                BranchId = branchId,
                SupplierId = supplierId,
                OrderNumber = await GenerateNumberAsync("PO", _context.PurchaseOrders),
                Status = "Draft",
                OrderDate = DateTime.Now,
                ExpectedDate = expectedDate,
                CreatedBy = user ?? "System",
                Notes = notes,
                CreatedOn = DateTime.Now
            };

            _context.PurchaseOrders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var item in items.Where(i => i.IngredientId > 0 && i.Quantity > 0))
            {
                _context.PurchaseOrderItems.Add(new PurchaseOrderItem
                {
                    PurchaseOrderId = order.Id,
                    IngredientId = item.IngredientId,
                    QuantityOrdered = item.Quantity,
                    UnitCost = item.UnitCost,
                    CreatedOn = DateTime.Now
                });
                order.TotalAmount += (decimal)item.Quantity * item.UnitCost;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return order;
        }

        public async Task<PurchaseOrder> UpdatePurchaseOrderStatusAsync(int purchaseOrderId, string status, string? user)
        {
            var order = await _context.PurchaseOrders.FindAsync(purchaseOrderId);
            if (order == null) throw new KeyNotFoundException("Purchase order not found.");

            var validStatuses = new[] { "Draft", "Pending", "Approved", "Received", "Cancelled" };
            if (!validStatuses.Contains(status)) throw new ArgumentException("Invalid purchase order status.");
            if (order.Status == "Received" && status != "Received") throw new InvalidOperationException("Received purchase orders cannot be moved backward.");

            order.Status = status;
            order.LastUpdatedOn = DateTime.Now;
            if (status == "Approved") order.ApprovedBy = user ?? "System";
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<PurchaseOrder> ReceivePurchaseOrderAsync(int purchaseOrderId, List<PurchaseOrderReceiveItemDto>? items, string? user)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            var order = await _context.PurchaseOrders
                .Include(p => p.Items)
                    .ThenInclude(i => i.Ingredient)
                .FirstOrDefaultAsync(p => p.Id == purchaseOrderId);
            if (order == null) throw new KeyNotFoundException("Purchase order not found.");
            if (order.Status == "Cancelled" || order.Status == "Received") throw new InvalidOperationException($"Cannot receive a purchase order in '{order.Status}' status.");

            foreach (var poItem in order.Items)
            {
                var received = items?.FirstOrDefault(i => i.PurchaseOrderItemId == poItem.Id)?.QuantityReceived ?? poItem.QuantityOrdered;
                if (received <= 0) continue;

                var ingredient = poItem.Ingredient ?? await _context.Ingredients.FindAsync(poItem.IngredientId);
                if (ingredient == null) continue;

                var stockBefore = ingredient.CurrentStock;
                ingredient.CurrentStock += received;
                ingredient.CostPerUnit = poItem.UnitCost;
                poItem.QuantityReceived += received;

                _context.InventoryAdjustments.Add(new InventoryAdjustment
                {
                    BranchId = order.BranchId,
                    IngredientId = ingredient.Id,
                    QuantityAdjusted = received,
                    Type = "Purchase",
                    Notes = $"Received purchase order {order.OrderNumber}",
                    AdjustmentDate = DateTime.Now,
                    AdjustedBy = user ?? "System"
                });
                AddMovement(order.BranchId, ingredient, "Purchase", received, stockBefore, ingredient.CurrentStock, poItem.UnitCost, order.OrderNumber, user, $"Received purchase order {order.OrderNumber}");
            }

            order.Status = "Received";
            order.ReceivedBy = user ?? "System";
            order.ReceivedDate = DateTime.Now;
            order.LastUpdatedOn = DateTime.Now;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return order;
        }

        public async Task<(IEnumerable<InventoryMovement> Items, int TotalCount)> GetMovementsAsync(InventoryMovementFilter filter)
        {
            var page = filter.Page <= 0 ? 1 : filter.Page;
            var pageSize = filter.PageSize <= 0 ? 25 : Math.Min(filter.PageSize, 200);
            var query = _context.InventoryMovements
                .Include(m => m.Branch)
                .Include(m => m.Ingredient)
                .AsQueryable();

            if (filter.BranchId.HasValue) query = query.Where(m => m.BranchId == filter.BranchId.Value);
            if (filter.IngredientId.HasValue) query = query.Where(m => m.IngredientId == filter.IngredientId.Value);
            if (!string.IsNullOrWhiteSpace(filter.MovementType)) query = query.Where(m => m.MovementType == filter.MovementType);
            if (filter.StartDate.HasValue) query = query.Where(m => m.MovementDate >= filter.StartDate.Value.Date);
            if (filter.EndDate.HasValue) query = query.Where(m => m.MovementDate < filter.EndDate.Value.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(m =>
                    (m.Ingredient != null && m.Ingredient.Name.Contains(search)) ||
                    (m.ReferenceNumber != null && m.ReferenceNumber.Contains(search)) ||
                    (m.Notes != null && m.Notes.Contains(search)));
            }

            var count = await query.CountAsync();
            var rows = await query.OrderByDescending(m => m.MovementDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (rows, count);
        }

        public async Task<InventoryCount> CreateInventoryCountAsync(int branchId, List<InventoryCountItemDto> items, bool requiresApproval, string? notes, string? user)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Inventory count must include at least one item.");

            var ingredients = await _context.Ingredients.Where(i => i.BranchId == branchId).ToListAsync();
            var count = new InventoryCount
            {
                BranchId = branchId,
                CountNumber = await GenerateNumberAsync("CNT", _context.InventoryCounts),
                Status = requiresApproval ? "PendingApproval" : "Approved",
                CountDate = DateTime.Now,
                RequiresApproval = requiresApproval,
                CountedBy = user ?? "System",
                Notes = notes,
                CreatedOn = DateTime.Now
            };
            _context.InventoryCounts.Add(count);
            await _context.SaveChangesAsync();

            foreach (var item in items)
            {
                var ingredient = ingredients.FirstOrDefault(i => i.Id == item.IngredientId);
                if (ingredient == null) continue;
                var variance = item.ActualQuantity - ingredient.CurrentStock;
                _context.InventoryCountItems.Add(new InventoryCountItem
                {
                    InventoryCountId = count.Id,
                    IngredientId = ingredient.Id,
                    ExpectedQuantity = ingredient.CurrentStock,
                    ActualQuantity = item.ActualQuantity,
                    Variance = variance,
                    Reason = item.Reason,
                    CreatedOn = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            return count;
        }

        public async Task<InventoryCount> ApproveInventoryCountAsync(int countId, string? user)
        {
            var count = await _context.InventoryCounts.FindAsync(countId);
            if (count == null) throw new KeyNotFoundException("Inventory count not found.");
            count.Status = "Approved";
            count.ApprovedBy = user ?? "System";
            count.ApprovedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return count;
        }

        public async Task<InventoryCount> ApplyInventoryCountAsync(int countId, string? user)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            var count = await _context.InventoryCounts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Ingredient)
                .FirstOrDefaultAsync(c => c.Id == countId);
            if (count == null) throw new KeyNotFoundException("Inventory count not found.");
            if (count.Status != "Approved") throw new InvalidOperationException("Inventory count must be approved before applying.");

            foreach (var item in count.Items.Where(i => Math.Abs(i.Variance) > 0.0001))
            {
                var ingredient = item.Ingredient ?? await _context.Ingredients.FindAsync(item.IngredientId);
                if (ingredient == null) continue;
                var stockBefore = ingredient.CurrentStock;
                ingredient.CurrentStock = item.ActualQuantity;

                _context.InventoryAdjustments.Add(new InventoryAdjustment
                {
                    BranchId = count.BranchId,
                    IngredientId = ingredient.Id,
                    QuantityAdjusted = item.Variance,
                    Type = "StockCount",
                    Notes = item.Reason,
                    AdjustmentDate = DateTime.Now,
                    AdjustedBy = user ?? "System"
                });
                AddMovement(count.BranchId, ingredient, "Adjustment", item.Variance, stockBefore, ingredient.CurrentStock, ingredient.CostPerUnit, count.CountNumber, user, item.Reason);
            }

            count.Status = "Applied";
            count.AppliedDate = DateTime.Now;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return count;
        }

        public async Task<WasteLog> RecordWasteAsync(int branchId, int ingredientId, double quantity, string reason, string? notes, string? user)
        {
            await AdjustStockAsync(branchId, ingredientId, -Math.Abs(quantity), reason, notes, user);
            return await _context.WasteLogs
                .Include(w => w.Ingredient)
                .Where(w => w.BranchId == branchId && w.IngredientId == ingredientId)
                .OrderByDescending(w => w.WasteDate)
                .FirstAsync();
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

        private void AddMovement(int branchId, Ingredient ingredient, string movementType, double quantity, double stockBefore, double stockAfter, decimal? unitCost, string? referenceNumber, string? user, string? notes)
        {
            _context.InventoryMovements.Add(new InventoryMovement
            {
                BranchId = branchId,
                IngredientId = ingredient.Id,
                MovementType = movementType,
                Quantity = quantity,
                StockBefore = stockBefore,
                StockAfter = stockAfter,
                UnitCost = unitCost,
                TotalCost = unitCost.HasValue ? (decimal)Math.Abs(quantity) * unitCost.Value : null,
                ReferenceNumber = referenceNumber,
                UserName = user ?? "System",
                Notes = notes,
                MovementDate = DateTime.Now,
                CreatedOn = DateTime.Now
            });
        }

        private static string GetMovementType(string type)
        {
            if (type.Equals("Waste", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("Expired", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("Damage", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("Spoiled", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("Lost", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("CustomerReturn", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("Theft", StringComparison.OrdinalIgnoreCase))
            {
                return "Waste";
            }

            if (type.Equals("StockCount", StringComparison.OrdinalIgnoreCase))
            {
                return "Adjustment";
            }

            return type;
        }

        private async Task<string> GenerateNumberAsync<T>(string prefix, DbSet<T> set) where T : class
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            var count = await set.CountAsync();
            return $"{prefix}-{today}-{count + 1:0000}";
        }
    }
}
