using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Birmarket.Api.Configuration;
using Birmarket.Api.Domain;
using Birmarket.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Birmarket.Tests;

[CollectionDefinition("Demo API", DisableParallelization = true)]
public sealed class DemoApiCollection : ICollectionFixture<DemoApplicationFactory> { }

[Collection("Demo API")]
public sealed class StoreApiTests(DemoApplicationFactory factory)
{
    [Fact]
    public async Task DemoCatalogUsesSeedDataAndHealthReportsDemoMode()
    {
        using var client = factory.CreateClient();
        var health = await client.GetFromJsonAsync<JsonElement>("/api/health/ready");
        var categories = await client.GetFromJsonAsync<JsonElement[]>("/api/catalog/categories");
        var products = await client.GetFromJsonAsync<JsonElement>("/api/catalog/products?q=seramik");

        Assert.Equal("demo", health.GetProperty("mode").GetString());
        Assert.Equal(3, categories!.Length);
        Assert.Contains(products.GetProperty("items").EnumerateArray(), item => item.GetProperty("name").GetString() == "Seramik Kahve Seti");
    }

    [Fact]
    public async Task DemoCheckoutReservesStockAndFailedPaymentRestoresIt()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var productPage = await client.GetFromJsonAsync<JsonElement>("/api/catalog/products?q=seramik");
        var product = productPage.GetProperty("items")[0];
        var productId = product.GetProperty("id").GetInt32();
        var initialStock = product.GetProperty("stock").GetInt32();
        var xsrf = await GetXsrfTokenAsync(client);

        var checkout = new
        {
            provider = "iyzico",
            customerName = "Deniz Demir",
            customerEmail = "deniz@example.com",
            customerPhone = "05551234567",
            addressLine = "Moda Caddesi 10",
            city = "İstanbul",
            district = "Kadıköy",
            postalCode = "34710",
            couponCode = (string?)null,
            notes = (string?)null,
            items = new[] { new { productId, quantity = 1 } }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(checkout) };
        request.Headers.Add("X-XSRF-TOKEN", xsrf);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var orderNumber = result.GetProperty("orderNumber").GetString()!;
        Assert.True(result.GetProperty("demoMode").GetBoolean());

        var reduced = await client.GetFromJsonAsync<JsonElement>("/api/catalog/products?q=seramik");
        Assert.Equal(initialStock - 1, reduced.GetProperty("items")[0].GetProperty("stock").GetInt32());

        using var paymentRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderNumber}/demo-payment")
        {
            Content = JsonContent.Create(new { success = false })
        };
        paymentRequest.Headers.Add("X-XSRF-TOKEN", xsrf);
        using var paymentResponse = await client.SendAsync(paymentRequest);
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);

        var restored = await client.GetFromJsonAsync<JsonElement>("/api/catalog/products?q=seramik");
        Assert.Equal(initialStock, restored.GetProperty("items")[0].GetProperty("stock").GetInt32());
    }

    [Fact]
    public async Task StateChangingApiRequestsRequireAntiforgeryToken()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "a@example.com", password = "invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DemoAdministratorCanLoadOrdersAndCreateCatalogProducts()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var token = await GetXsrfTokenAsync(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "admin@birmarket.local", password = "DemoAdmin!234", rememberMe = false })
        };
        login.Headers.Add("X-XSRF-TOKEN", token);
        using var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        token = await GetXsrfTokenAsync(client);
        using var orders = await client.GetAsync("/api/admin/orders");
        using var products = await client.GetAsync("/api/admin/products");
        Assert.Equal(HttpStatusCode.OK, orders.StatusCode);
        Assert.Equal(HttpStatusCode.OK, products.StatusCode);

        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/admin/products")
        {
            Content = JsonContent.Create(new
            {
                name = "Test Product",
                sku = "TEST-ADMIN-001",
                description = "Admin integration fixture",
                price = 125m,
                compareAtPrice = (decimal?)null,
                stock = 5,
                categoryId = 1,
                imageUrl = "",
                imageTone = "clay",
                weightKg = 0.5m,
                widthCm = 20m,
                lengthCm = 20m,
                heightCm = 10m,
                isFeatured = false,
                isActive = true
            })
        };
        create.Headers.Add("X-XSRF-TOKEN", token);
        using var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
    }

    [Fact]
    public async Task DemoCustomerCanRequestAndUseAPasswordResetToken()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var token = await GetXsrfTokenAsync(client);
        using var resetRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password/forgot")
        {
            Content = JsonContent.Create(new { email = "customer@birmarket.local" })
        };
        resetRequest.Headers.Add("X-XSRF-TOKEN", token);
        using var resetResponse = await client.SendAsync(resetRequest);
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        var reset = await resetResponse.Content.ReadFromJsonAsync<JsonElement>();
        var demoToken = reset.GetProperty("demoResetToken").GetString()!;

        using var updateRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password/reset")
        {
            Content = JsonContent.Create(new { email = "customer@birmarket.local", token = demoToken, newPassword = "NewDemoPassword!123" })
        };
        updateRequest.Headers.Add("X-XSRF-TOKEN", token);
        using var updateResponse = await client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "customer@birmarket.local", password = "NewDemoPassword!123", rememberMe = false })
        };
        loginRequest.Headers.Add("X-XSRF-TOKEN", token);
        using var loginResponse = await client.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public void KargoJetSignatureComparisonAcceptsOnlyTheExpectedSignature()
    {
        const string body = "{\"event\":\"shipment.delivered\"}";
        const string secret = "test-webhook-secret";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        Assert.True(OrderWorkflow.VerifyKargoJetSignature(Encoding.UTF8.GetBytes(body), signature, secret));
        Assert.False(OrderWorkflow.VerifyKargoJetSignature(Encoding.UTF8.GetBytes(body), signature, "wrong-secret"));
        Assert.False(OrderWorkflow.VerifyKargoJetSignature(Encoding.UTF8.GetBytes(body), "bad-signature", secret));
    }

    [Fact]
    public void PayTrCallbackSignatureUsesMerchantKeyAndSalt()
    {
        const string merchantId = "merchant";
        const string merchantKey = "private-key";
        const string merchantSalt = "private-salt";
        const string order = "BM260101001";
        const string status = "success";
        const string amount = "12345";
        var value = order + merchantSalt + status + amount;
        var hash = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(merchantKey), Encoding.UTF8.GetBytes(value)));
        var provider = new PayTrPaymentProvider(new HttpClient(), new AppRuntimeOptions
        {
            DemoMode = false,
            PayTrMerchantId = merchantId,
            PayTrMerchantKey = merchantKey,
            PayTrMerchantSalt = merchantSalt
        });

        Assert.True(provider.ValidatePayTrCallback(order, status, amount, hash));
        Assert.False(provider.ValidatePayTrCallback(order, status, amount, "invalid"));
    }

    [Fact]
    public async Task IyzicoRetrieveUsesCallbackTokenAndLeavesFraudReviewPending()
    {
        var capturedPath = string.Empty;
        var capturedAuthorization = string.Empty;
        var capturedBody = string.Empty;
        using var client = new HttpClient(new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedPath = request.RequestUri!.AbsolutePath;
            capturedAuthorization = request.Headers.Authorization?.Scheme ?? string.Empty;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    status = "success",
                    paymentStatus = "SUCCESS",
                    token = "checkout-token",
                    paymentId = "PAY-123",
                    conversationId = "BM261007-0123456789ABCDEF",
                    price = 25.50m,
                    paidPrice = 25.50m,
                    fraudStatus = 0
                })
            };
        }));
        var provider = new IyzicoPaymentProvider(client, new AppRuntimeOptions
        {
            DemoMode = false,
            PaymentEnvironment = "sandbox",
            IyzicoApiKey = "sandbox-key",
            IyzicoSecretKey = "sandbox-secret"
        });

        var result = await provider.ConfirmIyzicoAsync("BM261007-0123456789ABCDEF", "checkout-token", CancellationToken.None);

        Assert.True(result.Pending);
        Assert.False(result.Success);
        Assert.Equal("BM261007-0123456789ABCDEF", result.ConversationId);
        Assert.Equal(25.50m, result.Price);
        Assert.Contains("/payment/iyzipos/checkoutform/auth/ecom/detail", capturedPath, StringComparison.Ordinal);
        Assert.Equal("IYZWSv2", capturedAuthorization);
        Assert.Contains("checkout-token", capturedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IyzicoHostedCheckoutIncludesThePersistableTokenAndExactBasketTotal()
    {
        var order = CreatePaymentTestOrder();
        var requestBody = string.Empty;
        using var client = new HttpClient(new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    status = "success",
                    token = "new-checkout-token",
                    paymentPageUrl = "https://sandbox-cpp.iyzipay.com/checkoutform?token=new-checkout-token"
                })
            };
        }));
        var provider = new IyzicoPaymentProvider(client, new AppRuntimeOptions
        {
            DemoMode = false,
            PaymentEnvironment = "sandbox",
            IyzicoApiKey = "sandbox-key",
            IyzicoSecretKey = "sandbox-secret",
            PublicBaseUrl = "https://shop.example.test"
        });

        var result = await provider.StartAsync(new PaymentStartRequest(order, "127.0.0.1", "12345678901"), CancellationToken.None);
        using var body = JsonDocument.Parse(requestBody);
        var basket = body.RootElement.GetProperty("basketItems").EnumerateArray()
            .Sum(item => decimal.Parse(item.GetProperty("price").GetString()!, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal("new-checkout-token", result.ProviderToken);
        Assert.Equal("sandbox-cpp.iyzipay.com", new Uri(result.RedirectUrl!).Host);
        Assert.Equal(order.Total, basket);
        Assert.Equal("12345678901", body.RootElement.GetProperty("buyer").GetProperty("identityNumber").GetString());
    }

    [Fact]
    public async Task PayTrCheckoutUsesSandboxModeAndSignsTheServerCalculatedAmount()
    {
        var order = CreatePaymentTestOrder();
        var formBody = string.Empty;
        using var client = new HttpClient(new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            formBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { status = "success", token = "paytr-session-token" })
            };
        }));
        const string merchantId = "merchant-id";
        const string merchantKey = "merchant-key";
        const string merchantSalt = "merchant-salt";
        const string clientIp = "1.2.3.4";
        var provider = new PayTrPaymentProvider(client, new AppRuntimeOptions
        {
            DemoMode = false,
            PaymentEnvironment = "sandbox",
            PayTrMerchantId = merchantId,
            PayTrMerchantKey = merchantKey,
            PayTrMerchantSalt = merchantSalt,
            PublicBaseUrl = "https://shop.example.test"
        });

        var result = await provider.StartAsync(new PaymentStartRequest(order, clientIp), CancellationToken.None);
        var form = ParseForm(formBody);
        var hashInput = string.Concat(merchantId, clientIp, order.Number, order.CustomerEmail, "19300", form["user_basket"], "0", "0", "TL", "1", merchantSalt);
        var expectedToken = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(merchantKey), Encoding.UTF8.GetBytes(hashInput)));

        Assert.Equal("1", form["test_mode"]);
        Assert.Equal("19300", form["payment_amount"]);
        Assert.Equal(expectedToken, form["paytr_token"]);
        Assert.Equal("www.paytr.com", new Uri(result.EmbedUrl!).Host);
    }

    [Fact]
    public void RealModeRejectsIncompleteOrSharedEnvironmentConfiguration()
    {
        var incomplete = new AppRuntimeOptions
        {
            DemoMode = false,
            DemoDatabasePath = "C:/store/demo.sqlite3",
            RealDatabasePath = "C:/store/live.sqlite3"
        };
        var missing = incomplete.MissingRealSettings();
        Assert.Contains("iyzico_api_key", missing);
        Assert.Contains("paytr_merchant_key", missing);
        Assert.Contains("birmarket_data_protection_key", missing);

        var sharedFiles = new AppRuntimeOptions
        {
            DemoMode = true,
            DemoDatabasePath = "C:/store/birmarket.sqlite3",
            RealDatabasePath = "C:/store/birmarket.sqlite3"
        };
        Assert.Contains(sharedFiles.MissingRealSettings(), setting => setting.Contains("must be different", StringComparison.Ordinal));
    }

    [Fact]
    public void ProviderBasketMatchesDiscountedOrderTotalAndShipping()
    {
        var order = new StoreOrder
        {
            Subtotal = 160,
            Discount = 16,
            Shipping = 49,
            Total = 193,
            Items =
            [
                new StoreOrderItem { SKU = "SKU-1", ProductName = "Product One", UnitPrice = 100, Quantity = 1 },
                new StoreOrderItem { SKU = "SKU-2", ProductName = "Product Two", UnitPrice = 30, Quantity = 2 }
            ]
        };

        var lines = ProviderBasketBuilder.Build(order);

        Assert.Equal(order.Total, lines.Sum(line => line.Amount));
        Assert.Equal(49m, lines[^1].Amount);
        Assert.Equal("VIRTUAL", lines[^1].ItemType);
    }

    private static async Task<string> GetXsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var header = response.Headers.GetValues("Set-Cookie").First(value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        return header["XSRF-TOKEN=".Length..].Split(';', 2)[0];
    }

    private static StoreOrder CreatePaymentTestOrder() => new()
    {
        Number = "BM261007-0123456789ABCDEF",
        CustomerName = "Deniz Demir",
        CustomerEmail = "deniz@example.com",
        CustomerPhone = "05551234567",
        AddressLine = "Moda Caddesi 10",
        City = "İstanbul",
        District = "Kadıköy",
        PostalCode = "34710",
        Subtotal = 160,
        Discount = 16,
        Shipping = 49,
        Total = 193,
        Items =
        [
            new StoreOrderItem { ProductId = 1, ProductName = "Product One", SKU = "SKU-1", UnitPrice = 100, Quantity = 1, WeightKg = 0.5m, WidthCm = 20, LengthCm = 20, HeightCm = 10 },
            new StoreOrderItem { ProductId = 2, ProductName = "Product Two", SKU = "SKU-2", UnitPrice = 30, Quantity = 2, WeightKg = 0.5m, WidthCm = 20, LengthCm = 20, HeightCm = 10 }
        ]
    };

    private static Dictionary<string, string> ParseForm(string formBody) => formBody.Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .ToDictionary(pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')), pair => Uri.UnescapeDataString(pair.Length > 1 ? pair[1].Replace('+', ' ') : ""));

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responseFactory(request, cancellationToken);
    }
}
