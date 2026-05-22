using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentHubPro.Data.Entities;

namespace RentHubPro.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp, IConfiguration config)
    {
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();

        foreach (var role in Roles.All)
            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole(role));

        var adminLogin = config["AdminAccount:Login"] ?? "admin";
        var adminEmail = config["AdminAccount:Email"] ?? "admin@renthub.local";
        var adminPassword = config["AdminAccount:Password"] ?? "Admin#2026!";

        var admin = await userMgr.FindByNameAsync(adminLogin);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminLogin,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Администратор системы",
                Theme = AppTheme.Light
            };
            var result = await userMgr.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
                await userMgr.AddToRoleAsync(admin, Roles.Admin);
        }

        if (!await db.Premises.AnyAsync())
            await SeedDemoDataAsync(userMgr, db);
    }

    private static async Task SeedDemoDataAsync(UserManager<ApplicationUser> userMgr, ApplicationDbContext db)
    {
        var landlord = new ApplicationUser
        {
            UserName = "+375291112233",
            PhoneNumber = "+375291112233",
            Email = "landlord@demo.by",
            EmailConfirmed = true,
            FullName = "Иван Арендодатель",
            Theme = AppTheme.Light
        };
        if (await userMgr.FindByNameAsync(landlord.UserName) is null)
        {
            await userMgr.CreateAsync(landlord, "Demo#2026!");
            await userMgr.AddToRoleAsync(landlord, Roles.Landlord);
        }

        var tenant = new ApplicationUser
        {
            UserName = "+375294445566",
            PhoneNumber = "+375294445566",
            Email = "tenant@demo.by",
            EmailConfirmed = true,
            FullName = "Пётр Арендатор",
            Theme = AppTheme.Light
        };
        if (await userMgr.FindByNameAsync(tenant.UserName) is null)
        {
            await userMgr.CreateAsync(tenant, "Demo#2026!");
            await userMgr.AddToRoleAsync(tenant, Roles.Tenant);
        }

        var premises = new[]
        {
            new Premise
            {
                Title = "Светлый офис в центре", Type = PremiseType.Office, Area = 64.5, Floor = 3,
                PricePerSquareMeter = 18.50m, Address = "г. Минск, пр. Независимости, 25",
                Description = "Просторный офис с панорамными окнами, готов к заселению. Кондиционер, оптоволокно, охрана.",
                Status = PremiseStatus.Available, LandlordId = landlord.Id
            },
            new Premise
            {
                Title = "Торговое помещение у метро", Type = PremiseType.Retail, Area = 120, Floor = 1,
                PricePerSquareMeter = 30m, Address = "г. Минск, ул. Немига, 3",
                Description = "Высокий трафик, отдельный вход, витринные окна. Идеально под магазин или кафе.",
                Status = PremiseStatus.Available, LandlordId = landlord.Id
            },
            new Premise
            {
                Title = "Складское помещение", Type = PremiseType.Warehouse, Area = 350, Floor = 1,
                PricePerSquareMeter = 7m, Address = "г. Минск, ул. Селицкого, 17",
                Description = "Отапливаемый склад с пандусом, удобный подъезд для фур, видеонаблюдение.",
                Status = PremiseStatus.UnderRepair, LandlordId = landlord.Id
            }
        };
        db.Premises.AddRange(premises);
        await db.SaveChangesAsync();
    }
}
