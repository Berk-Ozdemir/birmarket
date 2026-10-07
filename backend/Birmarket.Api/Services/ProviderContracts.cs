using Birmarket.Api.Domain;

namespace Birmarket.Api.Services;

public sealed record PaymentStartRequest(StoreOrder Order, string ClientIp, string? IdentityNumber = null);
public sealed record PaymentStartResult(string? RedirectUrl, string? EmbedUrl, string ProviderReference, bool IsDemo = false, string? ProviderToken = null);
public sealed record PaymentConfirmation(bool Success, bool Pending, string? TransactionId, string EventKey, string? ConversationId, decimal? Price);
public sealed record ShipmentResult(string ShipmentId, string? TrackingNumber, string Status);
public sealed record ProviderBasketLine(string Id, string Name, decimal Amount, string Category, string ItemType);

public static class ProviderBasketBuilder
{
    public static IReadOnlyList<ProviderBasketLine> Build(StoreOrder order)
    {
        var grossLines = order.Items.Select(item => (Item: item, Gross: item.UnitPrice * item.Quantity)).ToArray();
        var productTotalCents = decimal.ToInt64(decimal.Round((order.Subtotal - order.Discount) * 100, 0, MidpointRounding.AwayFromZero));
        var remainingCents = productTotalCents;
        var result = new List<ProviderBasketLine>();
        for (var index = 0; index < grossLines.Length; index++)
        {
            var (item, gross) = grossLines[index];
            var amountCents = index == grossLines.Length - 1
                ? remainingCents
                : order.Subtotal == 0 ? 0 : decimal.ToInt64(decimal.Floor(gross / order.Subtotal * productTotalCents));
            remainingCents -= amountCents;
            var amount = amountCents / 100m;
            if (amount > 0)
            {
                var name = item.Quantity > 1 ? $"{item.ProductName} × {item.Quantity}" : item.ProductName;
                result.Add(new ProviderBasketLine(item.SKU, name, amount, "Birmarket", "PHYSICAL"));
            }
        }
        if (order.Shipping > 0)
            result.Add(new ProviderBasketLine("SHIPPING", "Kargo ücreti", order.Shipping, "Kargo", "VIRTUAL"));
        return result;
    }
}

public interface IPaymentProvider
{
    string Name { get; }
    Task<PaymentStartResult> StartAsync(PaymentStartRequest request, CancellationToken cancellationToken);
    Task<PaymentConfirmation> ConfirmIyzicoAsync(string conversationId, string token, CancellationToken cancellationToken);
    bool ValidatePayTrCallback(string merchantOid, string status, string totalAmount, string hash);
}

public interface IShippingProvider
{
    Task<ShipmentResult> CreateAsync(StoreOrder order, CancellationToken cancellationToken);
}

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken);
    Task SendOrderConfirmationAsync(StoreOrder order, CancellationToken cancellationToken);
}

public sealed class PaymentProviderRegistry(IEnumerable<IPaymentProvider> providers)
{
    public IPaymentProvider Get(string name) => providers.FirstOrDefault(x =>
        string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("Desteklenmeyen ödeme sağlayıcısı.");
}
