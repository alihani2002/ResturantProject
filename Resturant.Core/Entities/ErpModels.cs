using Resturant.Core.Common;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Resturant.Core.Entities
{
    // Financials: Expense Entity
    public class Expense : BaseEntity
    {
        [Required]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } // "Ingredients", "Labor", "Utilities", "Rent", "Marketing", "Waste", "Other"

        [Required]
        public DateTime ExpenseDate { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? MerchantName { get; set; }

        [MaxLength(100)]
        public string? PaymentMethod { get; set; }
    }

    // Inventory: Supplier Entity
    public class Supplier : BaseEntity
    {
        [Required]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        public double LeadTimeDays { get; set; } = 2.5;

        public double QualityRating { get; set; } = 4.5; // 1.0 to 5.0

        public System.Collections.Generic.ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new System.Collections.Generic.List<PurchaseOrder>();
    }

    // Inventory: Purchase Order header
    public class PurchaseOrder : BaseEntity
    {
        [Required]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        [Required]
        [MaxLength(30)]
        public string OrderNumber { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Draft"; // Draft, Pending, Approved, Received, Cancelled

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? ExpectedDate { get; set; }
        public DateTime? ReceivedDate { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        [MaxLength(100)]
        public string? ReceivedBy { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public System.Collections.Generic.ICollection<PurchaseOrderItem> Items { get; set; } = new System.Collections.Generic.List<PurchaseOrderItem>();
    }

    // Inventory: Purchase Order line
    public class PurchaseOrderItem : BaseEntity
    {
        [Required]
        public int PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }

        [Required]
        public int IngredientId { get; set; }
        public Ingredient? Ingredient { get; set; }

        [Required]
        public double QuantityOrdered { get; set; }

        public double QuantityReceived { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        [NotMapped]
        public decimal LineTotal => (decimal)QuantityOrdered * UnitCost;
    }

    // Inventory: Ingredient Entity (represents raw materials)
    public class Ingredient : BaseEntity
    {
        [Required]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        public double CurrentStock { get; set; }

        [Required]
        public double ReorderLevel { get; set; }

        [Required]
        [MaxLength(10)]
        public string Unit { get; set; } // "Kg", "Ltr", "Pcs", "Box"

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPerUnit { get; set; }

        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        public System.Collections.Generic.ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new System.Collections.Generic.List<PurchaseOrderItem>();
    }

    // Inventory: Recipe Entity (connects MenuItem/Product to Ingredients)
    public class Recipe : BaseEntity
    {
        [Required]
        public int MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        [Required]
        public int IngredientId { get; set; }
        public Ingredient? Ingredient { get; set; }

        [Required]
        public double QuantityRequired { get; set; } // Quantity of raw ingredient needed for 1 portion of MenuItem
    }

    // Inventory: WasteLog Entity
    public class WasteLog : BaseEntity
    {
        [Required]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        public int IngredientId { get; set; }
        public Ingredient? Ingredient { get; set; }

        [Required]
        public double QuantityWasted { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }

        [Required]
        [MaxLength(100)]
        public string Reason { get; set; } // "Expired", "Spoiled", "Burnt/CookError", "CustomerReturn"

        [Required]
        public DateTime WasteDate { get; set; } = DateTime.Now;
    }

    // Supply Chain: Stock Transfer Entity (moves stock between branches/warehouses)
    public class StockTransfer : BaseEntity
    {
        [Required]
        public int SourceBranchId { get; set; }
        public Branch? SourceBranch { get; set; }

        [Required]
        public int DestinationBranchId { get; set; }
        public Branch? DestinationBranch { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // "Pending", "Approved", "InTransit", "Received", "Cancelled"

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [MaxLength(100)]
        public string? ProcessedBy { get; set; }

        public DateTime? ProcessedDate { get; set; }

        [MaxLength(100)]
        public string? ReceivedBy { get; set; }

        public DateTime? ReceivedDate { get; set; }

        public System.Collections.Generic.ICollection<StockTransferItem> Items { get; set; } = new System.Collections.Generic.List<StockTransferItem>();
    }

    // Supply Chain: Stock Transfer Item Entity
    public class StockTransferItem : BaseEntity
    {
        [Required]
        public int StockTransferId { get; set; }
        public StockTransfer? StockTransfer { get; set; }

        [Required]
        [MaxLength(100)]
        public string IngredientName { get; set; }

        [Required]
        public double Quantity { get; set; }

        [Required]
        [MaxLength(10)]
        public string Unit { get; set; }
    }

    // Inventory: Manual/Automated Inventory Adjustment Entity
    public class InventoryAdjustment : BaseEntity
    {
        [Required]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        public int IngredientId { get; set; }
        public Ingredient? Ingredient { get; set; }

        [Required]
        public double QuantityAdjusted { get; set; } // positive or negative

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } // "StockCount", "Waste", "Damage", "Theft", "Expired", "Purchase", "Transfer"

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        public DateTime AdjustmentDate { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? AdjustedBy { get; set; }
    }

    // Inventory: Ledger row for every stock mutation.
    public class InventoryMovement : BaseEntity
    {
        [Required]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        public int IngredientId { get; set; }
        public Ingredient? Ingredient { get; set; }

        [Required]
        [MaxLength(50)]
        public string MovementType { get; set; } // Purchase, RecipeConsumption, TransferOut, TransferIn, Adjustment, Waste, Return

        [Required]
        public double Quantity { get; set; }

        [Required]
        public double StockBefore { get; set; }

        [Required]
        public double StockAfter { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? UnitCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalCost { get; set; }

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; }

        [MaxLength(100)]
        public string? UserName { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        public DateTime MovementDate { get; set; } = DateTime.Now;
    }

    // Inventory: Physical count header
    public class InventoryCount : BaseEntity
    {
        [Required]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        [MaxLength(30)]
        public string CountNumber { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Draft"; // Draft, PendingApproval, Approved, Applied, Cancelled

        [Required]
        public DateTime CountDate { get; set; } = DateTime.Now;

        public bool RequiresApproval { get; set; }

        [MaxLength(100)]
        public string? CountedBy { get; set; }

        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedDate { get; set; }

        public DateTime? AppliedDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public System.Collections.Generic.ICollection<InventoryCountItem> Items { get; set; } = new System.Collections.Generic.List<InventoryCountItem>();
    }

    // Inventory: Physical count line
    public class InventoryCountItem : BaseEntity
    {
        [Required]
        public int InventoryCountId { get; set; }
        public InventoryCount? InventoryCount { get; set; }

        [Required]
        public int IngredientId { get; set; }
        public Ingredient? Ingredient { get; set; }

        [Required]
        public double ExpectedQuantity { get; set; }

        [Required]
        public double ActualQuantity { get; set; }

        [Required]
        public double Variance { get; set; }

        [Required]
        [MaxLength(200)]
        public string Reason { get; set; }
    }

    // Sales: flexible product pricing levels and future pricing rules.
    public class MenuItemPrice : BaseEntity
    {
        [Required]
        public int MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        [MaxLength(50)]
        public string PriceType { get; set; } = "Retail"; // Retail, Wholesale, VIP, Delivery, Custom

        [MaxLength(100)]
        public string? PriceListName { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int? MinQuantity { get; set; }

        [MaxLength(100)]
        public string? CustomerKey { get; set; }

        public DateTime? StartsOn { get; set; }
        public DateTime? EndsOn { get; set; }
        public bool IsActive { get; set; } = true;
        public bool AllowOverride { get; set; } = true;
        public int Priority { get; set; } = 100;
    }
}
