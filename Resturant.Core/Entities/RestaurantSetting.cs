using Resturant.Core.Common;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class RestaurantSetting : BaseEntity
    {
        [Required]
        [Range(0, 100, ErrorMessage = "Tax percentage must be between 0 and 100")]
        public decimal TaxPercentage { get; set; } = 14;

        [Required]
        [Range(0, 100, ErrorMessage = "Service percentage must be between 0 and 100")]
        public decimal ServicePercentage { get; set; } = 12;
    }
}
