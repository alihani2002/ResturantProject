/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Common;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class MenuItemRecommendation : BaseEntity
    {
        [Required]
        public int PrimaryMenuItemId { get; set; }
        public MenuItem? PrimaryMenuItem { get; set; }

        [Required]
        public int RecommendedMenuItemId { get; set; }
        public MenuItem? RecommendedMenuItem { get; set; }
    }
}
