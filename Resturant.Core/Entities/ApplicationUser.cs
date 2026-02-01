using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Resturant.Core.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(100)]
        public string? FullName { get; set; }
        public override string? Email { get => base.Email; set => base.Email = value; }
        public bool IsCompelteProfile { get; set; } = false;
        public int? Age { get; set; }
        public bool IsDeleted { get; set; }
        public string? CreatedById { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public string? LastUpdatedById { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
        // public Client? ClientProfile { get; set; }
        public string Role { get; set; } = null!;
    }
}
