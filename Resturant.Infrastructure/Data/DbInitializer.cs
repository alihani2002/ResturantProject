using Microsoft.AspNetCore.Identity;
using Resturant.Core.Entities;
using System.Threading.Tasks;

namespace Resturant.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roleNames = { "Admin", "Chief", "Waiter", "User", "Manager", "Accountant" };

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }
    }
}
