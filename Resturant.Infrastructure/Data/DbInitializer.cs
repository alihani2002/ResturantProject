using Microsoft.AspNetCore.Identity;
using Resturant.Core.Common;
using Resturant.Core.Entities;
using System;
using System.Collections.Generic;
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

            // Seed Menu Categories
            if (!context.MenuCategories.Any())
            {
                var categories = new List<MenuCategory>
                {
                    new MenuCategory { Name = "Appetizers", Description = "Starters and small plates", IsActive = true },
                    new MenuCategory { Name = "Main Courses", Description = "Hearty and filling dishes", IsActive = true },
                    new MenuCategory { Name = "Desserts", Description = "Sweet treats and desserts", IsActive = true },
                    new MenuCategory { Name = "Beverages", Description = "Drinks and refreshments", IsActive = true }
                };
                context.MenuCategories.AddRange(categories);
                await context.SaveChangesAsync();
            }

            // Seed Menu Items
            if (!context.MenuItems.Any())
            {
                var appetizersCategory = context.MenuCategories.FirstOrDefault(c => c.Name == "Appetizers");
                var mainCoursesCategory = context.MenuCategories.FirstOrDefault(c => c.Name == "Main Courses");
                var dessertsCategory = context.MenuCategories.FirstOrDefault(c => c.Name == "Desserts");
                var beveragesCategory = context.MenuCategories.FirstOrDefault(c => c.Name == "Beverages");

                var menuItems = new List<MenuItem>
                {
                    new MenuItem { Name = "Caesar Salad", Description = "Fresh romaine lettuce with parmesan cheese and croutons", Price = 12.99m, IsAvailable = true, MenuCategoryId = appetizersCategory.Id },
                    new MenuItem { Name = "Bruschetta", Description = "Toasted bread with tomato, garlic, and basil", Price = 9.99m, IsAvailable = true, MenuCategoryId = appetizersCategory.Id },
                    new MenuItem { Name = "Grilled Steak", Description = "Juicy ribeye steak with mashed potatoes and vegetables", Price = 24.99m, IsAvailable = true, MenuCategoryId = mainCoursesCategory.Id },
                    new MenuItem { Name = "Margherita Pizza", Description = "Classic pizza with fresh mozzarella and basil", Price = 16.99m, IsAvailable = true, MenuCategoryId = mainCoursesCategory.Id },
                    new MenuItem { Name = "Chocolate Lava Cake", Description = "Warm chocolate cake with a molten center", Price = 8.99m, IsAvailable = true, MenuCategoryId = dessertsCategory.Id },
                    new MenuItem { Name = "Tiramisu", Description = "Classic Italian dessert with coffee-soaked ladyfingers", Price = 7.99m, IsAvailable = true, MenuCategoryId = dessertsCategory.Id },
                    new MenuItem { Name = "Espresso", Description = "Strong and rich Italian coffee", Price = 3.99m, IsAvailable = true, MenuCategoryId = beveragesCategory.Id },
                    new MenuItem { Name = "Iced Tea", Description = "Refreshing iced tea with lemon", Price = 2.99m, IsAvailable = true, MenuCategoryId = beveragesCategory.Id }
                };
                context.MenuItems.AddRange(menuItems);
                await context.SaveChangesAsync();
            }

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
