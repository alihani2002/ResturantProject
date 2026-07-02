using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
            string[] roleNames = { AppRoles.Admin, AppRoles.Chief, AppRoles.Waiter, AppRoles.User, AppRoles.Manager, AppRoles.Accountant, AppRoles.Driver };

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

            // Seed a Default Branch if none exist
            if (!await context.Branches.AnyAsync())
            {
                var defaultBranch = new Branch
                {
                    Name = "الفرع الرئيسي",
                    Address = "القاهرة، مصر",
                    ContactPhone = "01000000000",
                    IsActive = true
                };
                context.Branches.Add(defaultBranch);
                await context.SaveChangesAsync();
            }

            // Seed Default Delivery Zones if none exist
            if (!await context.DeliveryZones.AnyAsync())
            {
                var branches = await context.Branches.ToListAsync();
                foreach (var branch in branches)
                {
                    context.DeliveryZones.AddRange(
                        new DeliveryZone { Name = "المعادي", ArabicName = "المعادي", Governorate = "القاهرة", ArabicGovernorate = "القاهرة", DeliveryFee = 25.00m, IsActive = true, BranchId = branch.Id },
                        new DeliveryZone { Name = "التجمع الخامس", ArabicName = "التجمع الخامس", Governorate = "القاهرة", ArabicGovernorate = "القاهرة", DeliveryFee = 45.00m, IsActive = true, BranchId = branch.Id },
                        new DeliveryZone { Name = "مدينة نصر", ArabicName = "مدينة نصر", Governorate = "القاهرة", ArabicGovernorate = "القاهرة", DeliveryFee = 35.00m, IsActive = true, BranchId = branch.Id },
                        new DeliveryZone { Name = "المهندسين", ArabicName = "المهندسين", Governorate = "الجيزة", ArabicGovernorate = "الجيزة", DeliveryFee = 30.00m, IsActive = true, BranchId = branch.Id },
                        new DeliveryZone { Name = "الدقي", ArabicName = "الدقي", Governorate = "الجيزة", ArabicGovernorate = "الجيزة", DeliveryFee = 30.00m, IsActive = true, BranchId = branch.Id }
                    );
                }
                await context.SaveChangesAsync();
            }
        }
    }
}
