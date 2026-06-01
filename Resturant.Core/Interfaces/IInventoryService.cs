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
        
        // Automated consumption
        Task DeductStockForOrderAsync(int orderId);

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
}
