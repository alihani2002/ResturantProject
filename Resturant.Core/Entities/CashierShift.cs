using Resturant.Core.Common;
using System;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class CashierShift : BaseEntity
    {
        [Required]
        public string CashierId { get; set; } = string.Empty;
        public ApplicationUser? Cashier { get; set; }

        [Required]
        [Display(Name = "Branch")]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; } = true;

        [Display(Name = "Expected Shift Amount")]
        public decimal ExpectedAmount { get; set; }

        [Display(Name = "Actual Shift Amount Entered")]
        public decimal? ActualAmountEntered { get; set; }

        [Display(Name = "Difference (Deficit/Surplus)")]
        public decimal? Difference { get; set; }
    }
}
