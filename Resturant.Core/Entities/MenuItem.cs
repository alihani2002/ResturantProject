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

        [Display(Name = "Cost")]
        [Range(0, double.MaxValue, ErrorMessage = "Cost cannot be negative")]
        public decimal Cost { get; set; }

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

        [Display(Name = "Order Number")]
        public int OrderNumber { get; set; } = 0;

        [Required]
        [Display(Name = "Category")]
        public int MenuCategoryId { get; set; }
        public MenuCategory? MenuCategory { get; set; }

        [Required]
        [Display(Name = "Branch")]
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }
        public bool IsDeleted { get; set; } = false;

        public ICollection<MenuItemAddOn> AddOns { get; set; } = new List<MenuItemAddOn>();
        public ICollection<MenuItemRecommendation> Recommendations { get; set; } = new List<MenuItemRecommendation>();
        public ICollection<MenuItemSize> Sizes { get; set; } = new List<MenuItemSize>();

        public class SizeOption
        {
            public int? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public decimal Cost { get; set; }
        }

        public List<SizeOption> GetParsedSizes()
        {
            if (Sizes != null && Sizes.Count > 0)
            {
                return Sizes
                    .Where(s => !s.IsDeleted)
                    .OrderBy(s => s.Id)
                    .Select(s => new SizeOption { Id = s.Id, Name = s.Name, Price = s.Price, Cost = s.Cost })
                    .ToList();
            }

            var list = new List<SizeOption>();
            if (string.IsNullOrEmpty(Description)) return list;

            // Find the 'Sizes:' prefix anywhere in the description (supports multiline)
            var sizesMatch = System.Text.RegularExpressions.Regex.Match(
                Description,
                @"(?:^|\n|\r)\s*(?:sizes?|الاحجام|الحجم|الأحجام|الأحجام والأسعار)\s*:\s*([^\r\n]+(?:[\r\n]+(?!\s*(?:sizes?|$))[^\r\n]+)*)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            string cleanDesc = sizesMatch.Success ? sizesMatch.Groups[1].Value : Description;

            var parts = cleanDesc.Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^([a-zA-Z\s\u0600-\u06FF]+?)\s*[-:(=]+\s*([\d.]+)\s*(?:EGP|L\.E\.|E\.G\.P\.|LE|ج\.م)?\s*\)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var name = match.Groups[1].Value.Trim();
                    if (decimal.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price))
                    {
                        list.Add(new SizeOption { Name = name, Price = price, Cost = Cost });
                    }
                }
            }
            return list;
        }

        public string GetFormattedNameWithPrice(decimal orderItemPrice)
        {
            return GetFormattedNameWithSize(null, orderItemPrice);
        }

        public string GetFormattedNameWithSize(string? sizeName, decimal orderItemPrice)
        {
            if (!string.IsNullOrWhiteSpace(sizeName))
            {
                return $"{Name} ({sizeName})";
            }

            if (orderItemPrice == Price) return Name;

            var sizes = GetParsedSizes();
            var matchedSize = System.Linq.Enumerable.FirstOrDefault(sizes, s => s.Price == orderItemPrice);
            if (matchedSize != null)
            {
                return $"{Name} ({matchedSize.Name})";
            }
            return Name;
        }
    }
}
