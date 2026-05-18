/* 
 * NOTE: didn't create migration or database
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace Resturant.Web.UI.ViewModels
{
    public class WaiterTableViewModel
    {
        public int TableId { get; set; }
        public int TableNumber { get; set; }
        public int? ActiveSessionId { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<WaiterOrderViewModel> Orders { get; set; } = new List<WaiterOrderViewModel>();
        
        // Helper to determine aggregate status for coloring the card
        public string AggregateStatus 
        { 
            get 
            {
                if (Orders == null || !Orders.Any()) return "Empty";
                if (Orders.Any(o => o.Status == "Pending")) return "Pending";
                if (Orders.Any(o => o.Status == "Active" || o.Status == "Ready")) return "Active";
                return "Empty";
            }
        }
    }

    public class WaiterOrderViewModel
    {
        public int OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? OrderTime { get; set; }
        public string Note { get; set; }
        public string Status { get; set; } // "Pending", "Active", "Ready"
        public List<WaiterOrderItemViewModel> OrderItems { get; set; } = new List<WaiterOrderItemViewModel>();
    }

    public class WaiterOrderItemViewModel
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
        public string? AddOns { get; set; }
    }
}

