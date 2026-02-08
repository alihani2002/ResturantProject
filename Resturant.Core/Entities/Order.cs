using Resturant.Core.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public enum OrderStatus
    {
        [Display(Name = "Pending")]
        Pending,
        [Display(Name = "Confirmed by Waiter")]
        Confirmed,
        [Display(Name = "In Preparation")]
        InPreparation,
        [Display(Name = "Ready")]
        Ready,
        [Display(Name = "Served")]
        Served,
        [Display(Name = "Completed")]
        Completed,
        [Display(Name = "Cancelled")]
        Cancelled
    }

    public class Order : BaseEntity
    {
        [Required]
        [Display(Name = "Table Number")]
        public int TableNumber { get; set; }

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

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}