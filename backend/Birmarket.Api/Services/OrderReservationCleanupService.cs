using Birmarket.Api.Data;
using Birmarket.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Birmarket.Api.Services;

public sealed class OrderReservationCleanupService(IServiceScopeFactory scopeFactory, ILogger<OrderReservationCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            try { await ExpireReservationsAsync(stoppingToken); }
            catch (Exception exception) { logger.LogError("Expired order reservation cleanup failed: {ErrorType}", exception.GetType().Name); }
        }
    }

    private async Task ExpireReservationsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var pending = await db.Orders.Include(x => x.Items)
            .Where(x => x.PaymentStatus == "Pending")
            .ToListAsync(cancellationToken);
        var expired = pending.Where(x => x.ReservationExpiresAt <= now).ToList();
        foreach (var order in expired)
        {
            order.PaymentStatus = "Expired";
            order.Status = "Expired";
            foreach (var item in order.Items)
            {
                var product = await db.Products.FindAsync([item.ProductId], cancellationToken);
                if (product is not null) product.Stock += item.Quantity;
            }
            if (!string.IsNullOrWhiteSpace(order.CouponCode))
            {
                var coupon = await db.Coupons.FirstOrDefaultAsync(x => x.Code == order.CouponCode, cancellationToken);
                if (coupon is not null && coupon.Uses > 0) coupon.Uses--;
            }
            db.PaymentEvents.Add(new PaymentEvent { Provider = "reservation", EventKey = $"{order.Number}:expired", OrderNumber = order.Number });
        }
        if (expired.Count == 0) return;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
