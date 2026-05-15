/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Common;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class MenuItem : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        [Display(Name = "Item Name")]
        public string Name { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Price")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Is Available")]
        public bool IsAvailable { get; set; } = true;

        [Display(Name = "Is Popular")]
        public bool IsPopular { get; set; } = false;

        [Display(Name = "Is Trending")]
        public bool IsTrending { get; set; } = false;

        [Display(Name = "Is Recommended")]
        public bool IsRecommended { get; set; } = false;

        [Required]
        [Display(Name = "Category")]
        public int MenuCategoryId { get; set; }
        public MenuCategory? MenuCategory { get; set; }

        public ICollection<MenuItemAddOn> AddOns { get; set; } = new List<MenuItemAddOn>();
        public ICollection<MenuItemRecommendation> Recommendations { get; set; } = new List<MenuItemRecommendation>();
    }
}