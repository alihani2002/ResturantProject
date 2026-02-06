using System;

namespace Resturant.Web.UI.ViewModels
{
    public class WaiterTableViewModel
    {
        public int TableId { get; set; }
        public int TableNumber { get; set; }
        public int? CurrentOrderId { get; set; }
        public string Status { get; set; } // "Empty", "Pending", "Active"
        public decimal TotalAmount { get; set; }
        public DateTime? OrderTime { get; set; }
    }
}
