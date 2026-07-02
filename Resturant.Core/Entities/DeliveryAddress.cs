using Resturant.Core.Common;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class DeliveryAddress : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [Phone]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Governorate { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Area { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Street { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Building { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Floor { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ApartmentNumber { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [MaxLength(500)]
        public string? DeliveryInstructions { get; set; }
    }
}
