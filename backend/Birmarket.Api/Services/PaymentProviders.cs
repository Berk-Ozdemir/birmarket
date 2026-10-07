using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Birmarket.Api.Configuration;

namespace Birmarket.Api.Services;

public sealed class PayTrPaymentProvider(HttpClient http, AppRuntimeOptions options) : IPaymentProvider
{
    public string Name => "paytr";

    public async Task<PaymentStartResult> StartAsync(PaymentStartRequest request, CancellationToken cancellationToken)
    {
        var order = request.Order;
        var merchantOid = order.Number;
        var email = order.CustomerEmail;
        var amount = decimal.ToInt64(decimal.Round(order.Total * 100, 0, MidpointRounding.AwayFromZero));
        var amountText = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var basketLines = ProviderBasketBuilder.Build(order).Select(line => new object[]
        {
            line.Name,
            line.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            1
        });
        var basket = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(basketLines)));
        var noInstallment = "0";
        var maxInstallment = "0";
        var currency = "TL";
        var testMode = options.PaymentEnvironment == "sandbox" ? "1" : "0";
        var hashInput = string.Concat(options.PayTrMerchantId, request.ClientIp, merchantOid, email, amountText, basket,
            noInstallment, maxInstallment, currency, testMode, options.PayTrMerchantSalt);
        var paytrToken = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.PayTrMerchantKey), Encoding.UTF8.GetBytes(hashInput)));
        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = options.PayTrMerchantId,
            ["user_ip"] = request.ClientIp,
            ["merchant_oid"] = merchantOid,
            ["email"] = email,
            ["payment_amount"] = amountText,
            ["user_basket"] = basket,
            ["no_installment"] = noInstallment,
            ["max_installment"] = maxInstallment,
            ["currency"] = currency,
            ["test_mode"] = testMode,
            ["paytr_token"] = paytrToken,
            ["debug_on"] = "0",
            ["timeout_limit"] = "30",
            ["user_name"] = order.CustomerName,
            ["user_address"] = order.AddressLine,
            ["user_phone"] = order.CustomerPhone,
            ["merchant_ok_url"] = $"{options.PublicBaseUrl}/checkout/result?order={Uri.EscapeDataString(order.Number)}",
            ["merchant_fail_url"] = $"{options.PublicBaseUrl}/checkout/result?order={Uri.EscapeDataString(order.Number)}&failed=1",
            ["lang"] = "tr",
            ["iframe_v2"] = "1"
        };
        using var response = await http.PostAsync("https://www.paytr.com/odeme/api/get-token", new FormUrlEncodedContent(form), cancellationToken);
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (payload.RootElement.GetProperty("status").GetString() != "success")
            throw new InvalidOperationException("PayTR ödeme oturumu başlatılamadı.");
        var token = payload.RootElement.GetProperty("token").GetString()!;
        return new PaymentStartResult(null, $"https://www.paytr.com/odeme/guvenli/{Uri.EscapeDataString(token)}", merchantOid);
    }

    public Task<PaymentConfirmation> ConfirmIyzicoAsync(string conversationId, string token, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Bu doğrulama iyzico sağlayıcısına aittir.");

    public bool ValidatePayTrCallback(string merchantOid, string status, string totalAmount, string hash)
    {
        var calculated = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.PayTrMerchantKey),
            Encoding.UTF8.GetBytes(merchantOid + options.PayTrMerchantSalt + status + totalAmount)));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(calculated), Encoding.UTF8.GetBytes(hash));
    }
}

public sealed class IyzicoPaymentProvider(HttpClient http, AppRuntimeOptions options) : IPaymentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string Name => "iyzico";
    private string BaseUrl => options.PaymentEnvironment == "sandbox" ? "https://sandbox-api.iyzipay.com" : "https://api.iyzipay.com";

    public async Task<PaymentStartResult> StartAsync(PaymentStartRequest request, CancellationToken cancellationToken)
    {
        var order = request.Order;
        var basketItems = ProviderBasketBuilder.Build(order).Select(line => new
        {
            id = line.Id,
            name = line.Name,
            category1 = line.Category,
            itemType = line.ItemType,
            price = line.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
        }).ToArray();
        var buyerName = order.CustomerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var body = JsonSerializer.Serialize(new
        {
            locale = "tr",
            conversationId = order.Number,
            price = order.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            paidPrice = order.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            currency = "TRY",
            basketId = order.Number,
            paymentGroup = "PRODUCT",
            callbackUrl = $"{options.PublicBaseUrl}/api/payments/iyzico/callback",
            enabledInstallments = new[] { 1, 2, 3, 6, 9, 12 },
            buyer = new
            {
                id = order.UserId ?? order.Number,
                name = buyerName.FirstOrDefault() ?? "Birmarket",
                surname = buyerName.Length > 1 ? buyerName[1] : "Müşteri",
                gsmNumber = order.CustomerPhone,
                email = order.CustomerEmail,
                identityNumber = request.IdentityNumber,
                registrationAddress = order.AddressLine,
                ip = request.ClientIp,
                city = order.City,
                country = "Turkey",
                zipCode = order.PostalCode
            },
            shippingAddress = new { contactName = order.CustomerName, city = order.City, country = "Turkey", address = order.AddressLine, zipCode = order.PostalCode },
            billingAddress = new { contactName = order.CustomerName, city = order.City, country = "Turkey", address = order.AddressLine, zipCode = order.PostalCode },
            basketItems
        }, JsonOptions);
        var path = "/payment/iyzipos/checkoutform/initialize/auth/ecom";
        using var response = await SendSignedAsync(HttpMethod.Post, path, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = payload.RootElement;
        if (root.TryGetProperty("status", out var status) && status.GetString() != "success")
            throw new InvalidOperationException("iyzico ödeme formu başlatılamadı.");
        var redirect = root.TryGetProperty("paymentPageUrl", out var paymentPage) ? paymentPage.GetString() : null;
        return new PaymentStartResult(redirect, null, order.Number, ProviderToken: root.GetProperty("token").GetString());
    }

    public async Task<PaymentConfirmation> ConfirmIyzicoAsync(string conversationId, string token, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new { locale = "tr", conversationId, token }, JsonOptions);
        const string path = "/payment/iyzipos/checkoutform/auth/ecom/detail";
        using var response = await SendSignedAsync(HttpMethod.Post, path, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = payload.RootElement;
        var returnedToken = root.TryGetProperty("token", out var tokenProperty) ? tokenProperty.GetString() : null;
        if (!string.Equals(returnedToken, token, StringComparison.Ordinal))
            throw new InvalidOperationException("iyzico returned a different checkout token.");
        var requestStatus = root.TryGetProperty("status", out var requestStatusProperty) ? requestStatusProperty.GetString() : null;
        var paymentStatus = root.TryGetProperty("paymentStatus", out var statusProperty) ? statusProperty.GetString() : null;
        var fraudStatus = root.TryGetProperty("fraudStatus", out var fraudProperty) && fraudProperty.TryGetInt32(out var fraudValue) ? fraudValue : -1;
        var success = requestStatus == "success" && paymentStatus == "SUCCESS" && fraudStatus == 1;
        var pending = requestStatus == "success" && paymentStatus == "SUCCESS" && fraudStatus == 0;
        var transaction = root.TryGetProperty("paymentId", out var paymentId) ? paymentId.GetString() : token;
        decimal? price = null;
        if (root.TryGetProperty("price", out var priceProperty))
        {
            if (priceProperty.ValueKind == JsonValueKind.Number && priceProperty.TryGetDecimal(out var numericPrice)) price = numericPrice;
            else if (priceProperty.ValueKind == JsonValueKind.String && decimal.TryParse(priceProperty.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedPrice)) price = parsedPrice;
        }
        var conversation = root.TryGetProperty("conversationId", out var conversationProperty) ? conversationProperty.GetString() : null;
        return new PaymentConfirmation(success, pending, transaction, $"{token}:{paymentStatus}:{fraudStatus}", conversation, price);
    }

    public bool ValidatePayTrCallback(string merchantOid, string status, string totalAmount, string hash) => false;

    private async Task<HttpResponseMessage> SendSignedAsync(HttpMethod method, string path, string body, CancellationToken cancellationToken)
    {
        var randomKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture) + RandomNumberGenerator.GetInt32(100000, 999999);
        var payload = randomKey + path + body;
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.IyzicoSecretKey), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var authorizationText = $"apiKey:{options.IyzicoApiKey}&randomKey:{randomKey}&signature:{signature}";
        var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorizationText));
        using var request = new HttpRequestMessage(method, BaseUrl + path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("IYZWSv2", authorization);
        request.Headers.Add("x-iyzi-rnd", randomKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await http.SendAsync(request, cancellationToken);
    }
}

public sealed class DemoPaymentProvider : IPaymentProvider
{
    public string Name => "demo";
    public Task<PaymentStartResult> StartAsync(PaymentStartRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentStartResult(null, null, request.Order.Number, true));
    public Task<PaymentConfirmation> ConfirmIyzicoAsync(string conversationId, string token, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentConfirmation(true, false, "DEMO-" + token, token, conversationId, null));
    public bool ValidatePayTrCallback(string merchantOid, string status, string totalAmount, string hash) => false;
}
