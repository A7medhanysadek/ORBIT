using Microsoft.AspNetCore.Identity;
using OrbitBackend.Models;

namespace OrbitBackend.Data.Seeding
{
    public static class RoleSeeder
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

            string[] roles = { "Admin", "Streamer" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Seed default admin account
            var adminEmail = "admin@orbit.app";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new AppUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Admin",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRolesAsync(adminUser, new[] { "Admin", "Streamer" });
                }
            }
            else
            {
                // Ensure existing admin also has the Streamer role
                if (!await userManager.IsInRoleAsync(adminUser, "Streamer"))
                {
                    await userManager.AddToRoleAsync(adminUser, "Streamer");
                }
            }
        }
    }
}
