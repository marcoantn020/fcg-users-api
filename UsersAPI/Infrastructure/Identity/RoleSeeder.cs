using Microsoft.AspNetCore.Identity;

namespace UsersAPI.Infrastructure.Identity;

public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<Domain.Entity.AppUser>>();

        string[] roles = { "User", "Admin" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        // Criar admin default somente em DEV
        var env = services.GetRequiredService<IHostEnvironment>();

        if (env.IsDevelopment())
        {
            var adminEmail = "admin@fcg.local";
            var adminPassword = "Admin123!";

            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin is null)
            {
                var user = new Domain.Entity.AppUser
                {
                    Id = Guid.NewGuid(),
                    UserName = adminEmail,
                    Email = adminEmail,
                    DisplayName = "FCG Admin"
                };

                var result = await userManager.CreateAsync(user, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Admin");
                }
            }
        }
    }
}