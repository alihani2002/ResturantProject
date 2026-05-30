/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Common;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Resturant.Core.Entities
{
    public class RestaurantTable : BaseEntity
    {
        [Display(Name = "Table Number")]
        public int TableNumber { get; set; }

        [Display(Name = "QR Code Image URL")]
        public string? QrCodeImageUrl { get; set; }

        [Display(Name = "Assigned Waiter")]
        public string? WaiterId { get; set; }
        public ApplicationUser? Waiter { get; set; }

        [Required]
        [Display(Name = "Branch")]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        public ICollection<TableSession> Sessions { get; set; } = new List<TableSession>();
    }
}

