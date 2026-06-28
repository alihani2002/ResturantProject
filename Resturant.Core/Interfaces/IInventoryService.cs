using Resturant.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Resturant.Core.Interfaces
{
    public interface IInventoryService
    {
        // Ingredients & Stock Levels
        Task<IEnumerable<Ingredient>> GetStockLevelsAsync(int? branchId);
        Task<Ingredient> AddStockAsync(int branchId, int ingredientId, double quantity, decimal? cost, string? notes, string? user);
        Task<Ingredient> AdjustStockAsync(int branchId, int ingredientId, double quantityAdjusted, string type, string? notes, string? user);

        // Purchase Orders & Suppliers
        Task<IEnumerable<Supplier>> GetSuppliersAsync(int? branchId);
        Task<Supplier> SaveSupplierAsync(Supplier supplier);
        Task<IEnumerable<PurchaseOrder>> GetPurchaseOrdersAsync(int? branchId, string? status);
        Task<PurchaseOrder> CreatePurchaseOrderAsync(int branchId, int supplierId, List<PurchaseOrderItemDto> items, DateTime? expectedDate, string? notes, string? user);
        Task<PurchaseOrder> UpdatePurchaseOrderStatusAsync(int purchaseOrderId, string status, string? user);
        Task<PurchaseOrder> ReceivePurchaseOrderAsync(int purchaseOrderId, List<PurchaseOrderReceiveItemDto>? items, string? user);

        // Movement history, counts, waste and analytics
        Task<(IEnumerable<InventoryMovement> Items, int TotalCount)> GetMovementsAsync(InventoryMovementFilter filter);
        Task<InventoryCount> CreateInventoryCountAsync(int branchId, List<InventoryCountItemDto> items, bool requiresApproval, string? notes, string? user);
        Task<InventoryCount> ApproveInventoryCountAsync(int countId, string? user);
        Task<InventoryCount> ApplyInventoryCountAsync(int countId, string? user);
        Task<WasteLog> RecordWasteAsync(int branchId, int ingredientId, double quantity, string reason, string? notes, string? user);
        
        // Automated consumption
        Task DeductStockForOrderAsync(int orderId);
        Task DeductStockForShiftAsync(int shiftId);

        // Stock Transfers Workflow
        Task<IEnumerable<StockTransfer>> GetTransfersAsync(int? branchId);
        Task<StockTransfer> RequestTransferAsync(int sourceBranchId, int destinationBranchId, List<StockTransferItemDto> items, string? notes, string? user);
        Task<StockTransfer> ApproveTransferAsync(int transferId, string? user);
        Task<StockTransfer> ReceiveTransferAsync(int transferId, string? user);
        Task<StockTransfer> CancelTransferAsync(int transferId, string? user);

        // Recipe Configuration
        Task<IEnumerable<Recipe>> GetRecipesForMenuItemAsync(int menuItemId);
        Task SaveRecipeAsync(int menuItemId, List<RecipeItemDto> items);
    }

    public class StockTransferItemDto
    {
        public string IngredientName { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
    }

    public class RecipeItemDto
    {
        public string IngredientName { get; set; }
        public double QuantityRequired { get; set; }
        public string Unit { get; set; }
    }

    public class PurchaseOrderItemDto
    {
        public int IngredientId { get; set; }
        public double Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class PurchaseOrderReceiveItemDto
    {
        public int PurchaseOrderItemId { get; set; }
        public double QuantityReceived { get; set; }
    }

    public class InventoryMovementFilter
    {
        public int? BranchId { get; set; }
        public int? IngredientId { get; set; }
        public string? MovementType { get; set; }
        public string? Search { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class InventoryCountItemDto
    {
        public int IngredientId { get; set; }
        public double ActualQuantity { get; set; }
        public string Reason { get; set; }
    }
}
