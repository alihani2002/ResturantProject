using Microsoft.AspNetCore.Identity;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using System;
using System.Threading.Tasks;

namespace Resturant.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task SeedDataAsync(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, AppDbContext context)
        {
            string[] roleNames = { AppRoles.Admin, AppRoles.Chief, AppRoles.Waiter, AppRoles.User, AppRoles.Manager, AppRoles.Accountant };

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // =====================================================
            // All other seeding data has been removed.
            // Only Roles + Admin User remain (as requested).
            // =====================================================

            // Seed Admin User
            var adminEmail = "admin@resturant.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                var newAdmin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    Role = AppRoles.Admin,
                    EmailConfirmed = true,
                    CreatedOn = DateTime.Now,
                    IsActive = true,
                    IsCompelteProfile = true,
                    IsDeleted = false

                };

                var result = await userManager.CreateAsync(newAdmin, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, AppRoles.Admin);
                }
            }
            else
            {
                // Ensure Admin is always Active and Not Deleted
                if (!adminUser.IsActive || adminUser.IsDeleted)
                {
                    adminUser.IsActive = true;
                    adminUser.IsDeleted = false;
                    await userManager.UpdateAsync(adminUser);
                }
            }
        }
    }
}
