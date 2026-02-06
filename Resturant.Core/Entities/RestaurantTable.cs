using Resturant.Core.Common;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Resturant.Core.Entities
{
    public class RestaurantTable : BaseEntity
    {
        [Display(Name = "Table Number")]
        public int TableNumber { get; set; }
    }
}
