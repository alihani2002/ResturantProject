/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class TableSession : BaseEntity
    {
        [Required]
        public int TableId { get; set; }
        public RestaurantTable? Table { get; set; }

        [Required]
        [Display(Name = "Branch")]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        public string CustomerName { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        public string PriceCategory { get; set; } = "Retail";

        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
