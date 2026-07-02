using Resturant.Core.Common;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class DeliveryZone : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ArabicName { get; set; }

        [Required]
        [MaxLength(100)]
        public string Governorate { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ArabicGovernorate { get; set; }

        [Required]
        public decimal DeliveryFee { get; set; }

        public bool IsActive { get; set; } = true;

        public string? GeoJsonBoundary { get; set; }
        
        [Required]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }
    }
}
