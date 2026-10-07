using Birmarket.Api.Configuration;
using Birmarket.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Birmarket.Api.Data;

public sealed class DemoSeeder(AppDbContext db, UserManager<CustomerUser> users, RoleManager<IdentityRole> roles, AppRuntimeOptions options)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in new[] { "Customer", "Admin" })
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));

        if (options.DemoMode && !await db.Categories.AnyAsync(cancellationToken))
        {
            var home = new Category { Name = "Ev & Yaşam", Slug = "ev-yasam", Description = "Günlük yaşama iyi gelen detaylar", Accent = "#e6b76c" };
            var personal = new Category { Name = "Kişisel Bakım", Slug = "kisisel-bakim", Description = "Kendine ayırdığın güzel anlar", Accent = "#d79492" };
            var accessories = new Category { Name = "Aksesuar", Slug = "aksesuar", Description = "Küçük dokunuşlar, büyük farklar", Accent = "#98aaa0" };
            db.Categories.AddRange(home, personal, accessories);
            await db.SaveChangesAsync(cancellationToken);
            db.Products.AddRange(
                new Product { Name = "Seramik Kahve Seti", Slug = "seramik-kahve-seti", SKU = "BM-1001", Description = "El yapımı hissi veren, güne yumuşak bir başlangıç sunan iki parçalı kahve seti.", Price = 690, CompareAtPrice = 820, Stock = 18, CategoryId = home.Id, ImageTone = "clay", IsFeatured = true, WeightKg = 0.8m, WidthCm = 18, LengthCm = 18, HeightCm = 14 },
                new Product { Name = "Sahil Kokulu Mum", Slug = "sahil-kokulu-mum", SKU = "BM-1002", Description = "Sedir, tuzlu hava ve gün batımını hatırlatan soya mum.", Price = 420, Stock = 32, CategoryId = home.Id, ImageTone = "sea", IsFeatured = true, WeightKg = 0.4m, WidthCm = 9, LengthCm = 9, HeightCm = 12 },
                new Product { Name = "Gün Işığı Yüz Serumu", Slug = "gun-isigi-yuz-serumu", SKU = "BM-2001", Description = "Hafif dokulu, günlük bakım rutinine kolayca eklenen nem serumu.", Price = 760, CompareAtPrice = 890, Stock = 14, CategoryId = personal.Id, ImageTone = "rose", IsFeatured = true, WeightKg = 0.25m, WidthCm = 6, LengthCm = 6, HeightCm = 14 },
                new Product { Name = "Dokulu Keten Çanta", Slug = "dokulu-keten-canta", SKU = "BM-3001", Description = "Her gün yanında; geniş, hafif ve doğal dokulu.", Price = 980, Stock = 11, CategoryId = accessories.Id, ImageTone = "sage", IsFeatured = false, WeightKg = 0.5m, WidthCm = 34, LengthCm = 28, HeightCm = 5 },
                new Product { Name = "Ada Çizgili Fincan", Slug = "ada-cizgili-fincan", SKU = "BM-1003", Description = "Kahve molasına renk katan, elde şekillendirilmiş seramik fincan.", Price = 360, Stock = 22, CategoryId = home.Id, ImageTone = "blue", IsFeatured = false, WeightKg = 0.35m, WidthCm = 12, LengthCm = 12, HeightCm = 10 },
                new Product { Name = "Yumuşak Dokunuş El Kremi", Slug = "yumusa-dokunus-el-kremi", SKU = "BM-2002", Description = "Çantada taşımaya uygun, hafif kokulu günlük el kremi.", Price = 290, Stock = 0, CategoryId = personal.Id, ImageTone = "cream", IsFeatured = false, WeightKg = 0.2m, WidthCm = 5, LengthCm = 5, HeightCm = 12 }
            );
            db.Coupons.Add(new Coupon { Code = "MERHABA10", PercentOff = 10, MinimumSubtotal = 500, MaximumUses = 100, IsActive = true });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (options.DemoMode)
        {
            await RenameLegacyDemoCustomerAsync();
            await EnsureUserAsync("admin@birmarket.local", "Birmarket Mağaza Yöneticisi", "DemoAdmin!234", "Admin", cancellationToken);
            await EnsureUserAsync("customer@birmarket.local", "Örnek Müşteri", "DemoCustomer!234", "Customer", cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(options.AdminEmail))
        {
            await EnsureUserAsync(options.AdminEmail, "Mağaza Yöneticisi", options.AdminPassword, "Admin", cancellationToken);
        }
    }

    private async Task RenameLegacyDemoCustomerAsync()
    {
        const string oldEmail = "musteri@birmarket.local";
        const string newEmail = "customer@birmarket.local";
        if (await users.FindByEmailAsync(newEmail) is not null) return;
        var legacyUser = await users.FindByEmailAsync(oldEmail);
        if (legacyUser is null) return;
        legacyUser.Email = newEmail;
        legacyUser.UserName = newEmail;
        await users.UpdateAsync(legacyUser);
    }

    private async Task EnsureUserAsync(string email, string name, string password, string role, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new CustomerUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = name };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded) throw new InvalidOperationException("Başlangıç hesabı oluşturulamadı: " + string.Join(", ", created.Errors.Select(x => x.Code)));
        }
        if (!await users.IsInRoleAsync(user, role)) await users.AddToRoleAsync(user, role);
    }
}
