namespace Birmarket.Api.Contracts;

public sealed record RegisterRequest(string DisplayName, string Email, string Password);
public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record CheckoutLineRequest(int ProductId, int Quantity);
public sealed record CheckoutRequest(
    string Provider,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string AddressLine,
    string City,
    string District,
    string PostalCode,
    string? CouponCode,
    string? Notes,
    List<CheckoutLineRequest> Items,
    string? IdentityNumber = null,
    bool SaveAddress = false,
    int? SavedAddressId = null);
public sealed record DemoPaymentRequest(bool Success);
public sealed record ProductUpsertRequest(string Name, string SKU, string Description, decimal Price,
    decimal? CompareAtPrice, int Stock, int CategoryId, string ImageUrl, string ImageTone,
    decimal WeightKg, decimal WidthCm, decimal LengthCm, decimal HeightCm, bool IsFeatured, bool IsActive);
public sealed record CategoryUpsertRequest(string Name, string Description, string Accent);
public sealed record OrderStatusRequest(string Status);
public sealed record CouponUpsertRequest(string Code, decimal PercentOff, decimal? MinimumSubtotal,
    DateTimeOffset? ExpiresAt, int? MaximumUses, bool IsActive);
