using Resturant.Core.Common;
using System;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public enum SettlementStatus
    {
        Pending = 0,
        Settled = 1,
        Discrepancy = 2
    }

    public class DriverSettlement : BaseEntity
    {
        [Required]
        public int DriverId { get; set; }
        public Driver? Driver { get; set; }

        [Required]
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        [Required]
        public decimal ExpectedCash { get; set; }

        [Required]
        public decimal CollectedCash { get; set; }

        [Required]
        public SettlementStatus Status { get; set; } = SettlementStatus.Pending;

        public string? SettledById { get; set; }
        public ApplicationUser? SettledBy { get; set; }

        public DateTime? SettledAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
