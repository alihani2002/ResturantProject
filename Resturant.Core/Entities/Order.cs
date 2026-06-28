/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public enum OrderStatus
    {
        [Display(Name = "Initiated")]
        Initiated = 100,
        [Display(Name = "Pending")]
        Pending = 0,
        [Display(Name = "Confirmed by Waiter")]
        Confirmed = 1,
        [Display(Name = "In Preparation")]
        InPreparation,
        [Display(Name = "Ready")]
        Ready,
        [Display(Name = "Served")]
        Served,
        [Display(Name = "Completed")]
        Completed,
        [Display(Name = "Cancelled")]
        Cancelled,
        [Display(Name = "Paid")]
        Paid
    }

    public class Order : BaseEntity
    {
        [Required]
        [Display(Name = "Table Number")]
        public int TableNumber { get; set; }

        [Required]
        [Display(Name = "Branch")]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Display(Name = "Customer Name")]
        public string? CustomerName { get; set; }

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Price Category")]
        public string PriceCategory { get; set; } = "Retail";

        [Display(Name = "Table Session Id")]
        public int? TableSessionId { get; set; }

        [Display(Name = "Note")]
        public string? Note { get; set; }

        [Required]
        [Display(Name = "Order Status")]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Waiter Id")]
        public string? WaiterId { get; set; }

        [Display(Name = "Chef Id")]
        public string? ChefId { get; set; }

        [Display(Name = "Cashier Id")]
        public string? CashierId { get; set; }

        [Display(Name = "Order Date")]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Display(Name = "Confirmed Date")]
        public DateTime? ConfirmedDate { get; set; }

        [Display(Name = "Completed Date")]
        public DateTime? CompletedDate { get; set; }

        [Display(Name = "Cancelled Date")]
        public DateTime? CancelledDate { get; set; }

        public int? ShiftId { get; set; }
        public CashierShift? Shift { get; set; }

        [Display(Name = "Paid Amount")]
        public decimal? PaidAmount { get; set; }

        [Display(Name = "Change Returned")]
        public decimal? ChangeReturned { get; set; }

        [Display(Name = "Tips")]
        public decimal? Tips { get; set; }

        [Display(Name = "Tax Percentage")]
        public decimal TaxPercentage { get; set; }

        [Display(Name = "Service Percentage")]
        public decimal ServicePercentage { get; set; }

        [Display(Name = "Tax Amount")]
        public decimal TaxAmount { get; set; }

        [Display(Name = "Service Amount")]
        public decimal ServiceAmount { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
