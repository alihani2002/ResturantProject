/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Common;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class OrderItemAddOn : BaseEntity
    {
        [Required]
        public int OrderItemId { get; set; }
        public OrderItem? OrderItem { get; set; }

        [Required]
        public int MenuItemAddOnId { get; set; }
        public MenuItemAddOn? AddOn { get; set; }

        [Required]
        public decimal Price { get; set; }
    }
}
