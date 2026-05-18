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
    }

    // Inventory: Ingredient Entity (represents raw materials)
    public class Ingredient : BaseEntity
    {
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
}
