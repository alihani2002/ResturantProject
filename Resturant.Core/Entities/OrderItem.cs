/*
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Common;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Resturant.Core.Entities
{
    public class OrderItem : BaseEntity
    {
        [Required]
        public int OrderId { get; set; }
        public Order Order { get; set; }

        [Required]
        public int MenuItemId { get; set; }
        public MenuItem MenuItem { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; } = 1;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        public int? MenuItemSizeId { get; set; }
        public string? SizeName { get; set; }
        public decimal UnitCost { get; set; }

        public decimal Total => Quantity * (Price + (AddOns != null ? AddOns.Sum(a => a.Price) : 0));

        /// <summary>
        /// The amount that actually counts toward payment.
        /// Returns 0 if the item was cancelled by the waiter.
        /// </summary>
        public decimal EffectiveTotal => IsCancelled ? 0m : Total;

        public ICollection<OrderItemAddOn> AddOns { get; set; } = new List<OrderItemAddOn>();

        /// <summary>Waiter can cancel a single item without cancelling the full order.</summary>
        public bool IsCancelled { get; set; } = false;
    }
}
