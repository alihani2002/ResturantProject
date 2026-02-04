using Resturant.Core.Common;
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

        [Required]
        [Display(Name = "Category")]
        public int MenuCategoryId { get; set; }
        public MenuCategory? MenuCategory { get; set; }
    }
}