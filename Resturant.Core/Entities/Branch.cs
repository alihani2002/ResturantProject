using Resturant.Core.Common;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class Branch : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        [Display(Name = "Branch Name")]
        public string Name { get; set; }

        [MaxLength(200)]
        [Display(Name = "Branch Address")]
        public string? Address { get; set; }

        [MaxLength(20)]
        [Display(Name = "Contact Phone")]
        public string? ContactPhone { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        // Relationships
        public ICollection<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<ApplicationUser> Staff { get; set; } = new List<ApplicationUser>();
        public ICollection<MenuCategory> MenuCategories { get; set; } = new List<MenuCategory>();
    }
}
