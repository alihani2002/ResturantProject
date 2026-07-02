using Resturant.Core.Common;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public enum DriverStatus
    {
        Offline = 0,
        Idle = 1,
        OnDelivery = 2
    }

    public class Driver : BaseEntity
    {
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Phone]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public DriverStatus Status { get; set; } = DriverStatus.Offline;

        [MaxLength(50)]
        public string? VehicleNumber { get; set; }

        [Required]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }
    }
}
