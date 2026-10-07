using Microsoft.AspNetCore.Identity;

namespace Birmarket.Api.Domain;

public sealed class CustomerUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Accent { get; set; } = "#d98b5f";
    public List<Product> Products { get; set; } = [];
}

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int Stock { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageTone { get; set; } = "sand";
    public decimal WeightKg { get; set; } = 0.5m;
    public decimal WidthCm { get; set; } = 20;
    public decimal LengthCm { get; set; } = 20;
    public decimal HeightCm { get; set; } = 10;
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal PercentOff { get; set; }
    public decimal? MinimumSubtotal { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public int? MaximumUses { get; set; }
    public int Uses { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class StoreOrder
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public CustomerUser? User { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }
    public string? CouponCode { get; set; }
    public string PaymentProvider { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = "Pending";
    public string Status { get; set; } = "PendingPayment";
    public string? ProviderReference { get; set; }
    public string? ProviderToken { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ShipmentId { get; set; }
    public string? TrackingNumber { get; set; }
    public string ShippingStatus { get; set; } = "NotCreated";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ReservationExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddMinutes(30);
    public List<StoreOrderItem> Items { get; set; } = [];
}

public sealed class StoreOrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public StoreOrder? Order { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal WeightKg { get; set; }
    public decimal WidthCm { get; set; }
    public decimal LengthCm { get; set; }
    public decimal HeightCm { get; set; }
}

public sealed class PaymentEvent
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string EventKey { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EmailOutboxEntry
{
    public int Id { get; set; }
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WishlistItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CustomerAddress
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Label { get; set; } = "Ev";
    public string RecipientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
