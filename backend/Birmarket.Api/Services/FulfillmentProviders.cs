using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Birmarket.Api.Configuration;
using Birmarket.Api.Data;
using Birmarket.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Birmarket.Api.Services;

public sealed class KargoJetShippingProvider(HttpClient http, AppRuntimeOptions options) : IShippingProvider
{
    private string BaseUrl => options.PaymentEnvironment == "sandbox"
        ? "https://sandbox-api.kargojet.com/partner-api/v1"
        : "https://api.kargojet.com/partner-api/v1";

    public async Task<ShipmentResult> CreateAsync(StoreOrder order, CancellationToken cancellationToken)
    {
        var item = order.Items.FirstOrDefault();
        var body = JsonSerializer.Serialize(new
        {
            reference = order.Number,
            carrierCode = options.KargoJetCarrierCode,
            recipient = new { name = order.CustomerName, phone = order.CustomerPhone, city = order.City, district = order.District, addressLine = order.AddressLine },
            parcel = new
            {
                widthCm = item?.WidthCm ?? 20,
                lengthCm = item?.LengthCm ?? 20,
                heightCm = order.Items.Sum(x => x.HeightCm * x.Quantity),
                weightKg = order.Items.Sum(x => x.WeightKg * x.Quantity)
            }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/shipments")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.KargoJetToken);
        request.Headers.Add("Idempotency-Key", order.Number);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        return new ShipmentResult(root.GetProperty("id").GetString()!,
            root.TryGetProperty("trackingNumber", out var tracking) ? tracking.GetString() : null,
            root.TryGetProperty("status", out var status) ? status.GetString() ?? "label_created" : "label_created");
    }
}

public sealed class DemoShippingProvider : IShippingProvider
{
    public Task<ShipmentResult> CreateAsync(StoreOrder order, CancellationToken cancellationToken) =>
        Task.FromResult(new ShipmentResult("ÖRNEK-GÖNDERİ-" + order.Number, "ÖRNEK-" + RandomNumberGenerator.GetInt32(100000000, 999999999), "label_created"));
}

public sealed class SmtpEmailSender(AppDbContext db, AppRuntimeOptions options) : IEmailSender
{
    public Task SendOrderConfirmationAsync(StoreOrder order, CancellationToken cancellationToken)
    {
        var message = CreateMessage(order);
        return SendAsync(order.CustomerEmail, message.Subject, message.Body, cancellationToken);
    }

    public async Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var entry = new EmailOutboxEntry { To = recipient, Subject = subject, Body = htmlBody, Status = "Sending" };
        await db.EmailOutbox.AddAsync(entry, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            using var mail = new MailMessage(new MailAddress(options.EmailFrom, options.EmailFromName), new MailAddress(recipient))
            {
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            using var smtp = new SmtpClient(options.SmtpHost, options.SmtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword),
                Timeout = 12000
            };
            await smtp.SendMailAsync(mail, cancellationToken);
            entry.Status = "Sent";
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            entry.Status = "Failed";
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    internal static (string Subject, string Body) CreateMessage(StoreOrder order) =>
        ($"Birmarket siparişin alındı · {order.Number}",
         $"<h1>Teşekkürler, {WebUtility.HtmlEncode(order.CustomerName)}!</h1><p>{order.Number} numaralı siparişin hazırlandığında takip numaranı paylaşacağız.</p><p>Toplam: {order.Total:N2} TL</p>");
}

public sealed class DemoEmailSender(AppDbContext db) : IEmailSender
{
    public Task SendOrderConfirmationAsync(StoreOrder order, CancellationToken cancellationToken)
    {
        var message = SmtpEmailSender.CreateMessage(order);
        return SendAsync(order.CustomerEmail, message.Subject, message.Body, cancellationToken);
    }

    public async Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        await db.EmailOutbox.AddAsync(new EmailOutboxEntry { To = recipient, Subject = subject, Body = htmlBody, Status = "Simulated" }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class OrderWorkflow(AppDbContext db, IShippingProvider shipping, IEmailSender email)
{
    public async Task<bool> SettleAsync(string orderNumber, string provider, bool success, string? transactionId, string eventKey, CancellationToken cancellationToken, bool pending = false)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.PaymentEvents.AnyAsync(x => x.Provider == provider && x.EventKey == eventKey, cancellationToken))
            return false;
        var order = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Number == orderNumber, cancellationToken);
        if (order is null) return false;
        db.PaymentEvents.Add(new PaymentEvent { Provider = provider, EventKey = eventKey, OrderNumber = orderNumber });
        if (order.PaymentStatus is "Paid" or "Failed" or "Expired")
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        order.ProviderTransactionId = transactionId;
        if (pending)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        order.PaymentStatus = success ? "Paid" : "Failed";
        order.Status = success ? "Processing" : "PaymentFailed";
        if (!success)
        {
            foreach (var line in order.Items)
            {
                var product = await db.Products.FindAsync([line.ProductId], cancellationToken);
                if (product is not null) product.Stock += line.Quantity;
            }
            if (!string.IsNullOrWhiteSpace(order.CouponCode))
            {
                var coupon = await db.Coupons.FirstOrDefaultAsync(x => x.Code == order.CouponCode, cancellationToken);
                if (coupon is not null && coupon.Uses > 0) coupon.Uses--;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (success)
        {
            try
            {
                var current = await db.Orders.Include(x => x.Items).FirstAsync(x => x.Number == orderNumber, cancellationToken);
                var result = await shipping.CreateAsync(current, cancellationToken);
                current.ShipmentId = result.ShipmentId;
                current.TrackingNumber = result.TrackingNumber;
                current.ShippingStatus = result.Status;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Shipping creation failed for order {orderNumber}: {exception.GetType().Name}");
            }
            try
            {
                var current = await db.Orders.FirstAsync(x => x.Number == orderNumber, cancellationToken);
                await email.SendOrderConfirmationAsync(current, cancellationToken);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Order email failed for order {orderNumber}: {exception.GetType().Name}");
            }
        }
        return true;
    }

    public static bool VerifyKargoJetSignature(byte[] payload, string? signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secret)) return false;
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload)).ToLowerInvariant();
        try
        {
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
        }
        catch (ArgumentException) { return false; }
    }
}
