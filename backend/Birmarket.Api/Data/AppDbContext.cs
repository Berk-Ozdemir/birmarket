using Birmarket.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Birmarket.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<CustomerUser, IdentityRole, string>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<StoreOrder> Orders => Set<StoreOrder>();
    public DbSet<StoreOrderItem> OrderItems => Set<StoreOrderItem>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<EmailOutboxEntry> EmailOutbox => Set<EmailOutboxEntry>();
    public DbSet<WishlistItem> Wishlist => Set<WishlistItem>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Category>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.SKU).IsUnique();
        modelBuilder.Entity<Product>().Property(x => x.Price).HasPrecision(12, 2);
        modelBuilder.Entity<Product>().Property(x => x.CompareAtPrice).HasPrecision(12, 2);
        modelBuilder.Entity<Coupon>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Coupon>().Property(x => x.PercentOff).HasPrecision(5, 2);
        modelBuilder.Entity<StoreOrder>().HasIndex(x => x.Number).IsUnique();
        modelBuilder.Entity<StoreOrder>().HasIndex(x => x.ProviderReference).IsUnique();
        modelBuilder.Entity<StoreOrder>().HasIndex(x => x.ProviderToken).IsUnique();
        modelBuilder.Entity<StoreOrder>().Property(x => x.Subtotal).HasPrecision(12, 2);
        modelBuilder.Entity<StoreOrder>().Property(x => x.Discount).HasPrecision(12, 2);
        modelBuilder.Entity<StoreOrder>().Property(x => x.Shipping).HasPrecision(12, 2);
        modelBuilder.Entity<StoreOrder>().Property(x => x.Total).HasPrecision(12, 2);
        modelBuilder.Entity<StoreOrder>().HasMany(x => x.Items).WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<StoreOrder>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<StoreOrderItem>().Property(x => x.UnitPrice).HasPrecision(12, 2);
        modelBuilder.Entity<PaymentEvent>().HasIndex(x => new { x.Provider, x.EventKey }).IsUnique();
        modelBuilder.Entity<WishlistItem>().HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        modelBuilder.Entity<CustomerAddress>().HasIndex(x => new { x.UserId, x.IsDefault });
        modelBuilder.Entity<Product>().HasOne(x => x.Category).WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

    }
}
