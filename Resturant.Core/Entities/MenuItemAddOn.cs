/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Common;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class MenuItemAddOn : BaseEntity
    {
        [Required]
        public int MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        public decimal ExtraPrice { get; set; }

        public bool IsAvailable { get; set; } = true;
    }
}
