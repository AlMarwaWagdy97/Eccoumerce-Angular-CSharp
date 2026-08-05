using Ecommerce.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Presistence;

// Development-only seed data: the full permission catalog, a built-in
// "Super Admin" role with every permission, and one seeded admin account —
// mirrors the pattern in DataSeeder.cs for the customer side.
public static class AdminDataSeeder
{
    public const string SeedAdminEmail = "admin.tester@example.com";
    public const string SeedAdminPassword = "AdminTester@123";
    public const string SuperAdminRoleName = "Super Admin";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var (key, module, description) in PermissionKeys.Catalog)
        {
            if (!await context.Permissions.AnyAsync(x => x.Key == key))
                context.Permissions.Add(new Permission { Key = key, Module = module, Description = description });
        }
        await context.SaveChangesAsync();

        var superAdminRole = await context.AdminRoles
            .Include(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Name == SuperAdminRoleName);

        if (superAdminRole is null)
        {
            superAdminRole = new AdminRole { Name = SuperAdminRoleName, Description = "Full system access", IsSystem = true };
            context.AdminRoles.Add(superAdminRole);
        }

        var allPermissions = await context.Permissions.ToListAsync();
        var missingPermissions = allPermissions.Where(p => !superAdminRole.Permissions.Any(rp => rp.Id == p.Id));
        superAdminRole.Permissions.AddRange(missingPermissions);
        await context.SaveChangesAsync();

        if (!await context.Admins.AnyAsync(x => x.Email == SeedAdminEmail))
        {
            var admin = new Admin
            {
                FirstName = "Admin",
                LastName = "Tester",
                Email = SeedAdminEmail,
                AdminRoleId = superAdminRole.Id,
                IsActive = true,
            };
            admin.PasswordHash = new PasswordHasher<Admin>().HashPassword(admin, SeedAdminPassword);

            context.Admins.Add(admin);
            await context.SaveChangesAsync();
        }
    }
}
