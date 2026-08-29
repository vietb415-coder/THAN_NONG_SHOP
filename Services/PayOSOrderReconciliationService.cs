using System.Data;
using Microsoft.EntityFrameworkCore;
using PayOS;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Services;

public sealed class PayOSOrderReconciliationService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PayOSOrderReconciliationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Clamp(configuration.GetValue("PayOS:ReconciliationIntervalMinutes", 5), 1, 60);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        await ReconcileAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ReconcileAsync(stoppingToken);
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var clientId = configuration["PayOS:ClientId"] ?? Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID");
        var apiKey = configuration["PayOS:ApiKey"] ?? Environment.GetEnvironmentVariable("PAYOS_API_KEY");
        var checksumKey = configuration["PayOS:ChecksumKey"] ?? Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(checksumKey)) return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<THAN_NONG_SHOP_DbContext>();
            var timeoutMinutes = Math.Clamp(configuration.GetValue("PayOS:PendingOrderTimeoutMinutes", 30), 5, 1440);
            var cutoff = DateTime.Now.AddMinutes(-timeoutMinutes);
            var pendingOrders = await db.Oders.AsNoTracking()
                .Where(order => order.Status == OrderStatus.AwaitingPayment && order.PayOSOrderCode != null)
                .Where(order => order.OrderDate <= cutoff)
                .Select(order => new { order.Id, OrderCode = order.PayOSOrderCode!.Value })
                .Take(100)
                .ToListAsync(cancellationToken);

            var payOS = new PayOSClient(clientId, apiKey, checksumKey);
            foreach (var pending in pendingOrders)
            {
                try
                {
                    var paymentLink = await payOS.PaymentRequests.GetAsync(pending.OrderCode);
                    var status = paymentLink.Status.ToString();
                    if (string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase))
                    {
                        await UpdateOrderAsync(pending.Id, paid: true, cancellationToken);
                        continue;
                    }

                    if (!string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                    {
                        await payOS.PaymentRequests.CancelAsync(pending.OrderCode, "Payment timeout");
                    }
                    await UpdateOrderAsync(pending.Id, paid: false, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Không thể đối soát đơn PayOS {OrderCode}.", pending.OrderCode);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tác vụ đối soát đơn PayOS thất bại.");
        }
    }

    private async Task UpdateOrderAsync(int orderId, bool paid, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<THAN_NONG_SHOP_DbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var order = await db.Oders.FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);
        if (order == null || order.Status != OrderStatus.AwaitingPayment)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (paid)
        {
            order.Status = OrderStatus.Paid;
            var voucher = await db.PromotionVouchers.FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);
            if (voucher != null)
            {
                voucher.UsedAt = DateTime.UtcNow;
                db.PromotionVoucherEvents.Add(new PromotionVoucherEvent { VoucherId=voucher.Id, EventType=VoucherEventTypes.Used, UserName=voucher.UserName, OrderId=orderId, CreatedAt=DateTime.UtcNow, Note="Đối soát PayOS xác nhận thanh toán" });
            }
        }
        else
        {
            var quantities = await db.OderDetails.Where(detail => detail.OderId == orderId)
                .GroupBy(detail => detail.ProductId)
                .Select(group => new { ProductId = group.Key, Quantity = group.Sum(detail => detail.Quantity) })
                .ToListAsync(cancellationToken);
            var productIds = quantities.Select(item => item.ProductId).ToArray();
            var products = await db.Products.Where(product => productIds.Contains(product.Id)).ToListAsync(cancellationToken);
            foreach (var quantity in quantities)
            {
                var product = products.FirstOrDefault(candidate => candidate.Id == quantity.ProductId);
                if (product != null) product.stockQuantity += quantity.Quantity;
            }
            order.Status = OrderStatus.Cancelled;
            var voucher = await db.PromotionVouchers.FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);
            if (voucher != null)
            {
                db.PromotionVoucherEvents.Add(new PromotionVoucherEvent { VoucherId=voucher.Id, EventType=VoucherEventTypes.Released, UserName=voucher.UserName, OrderId=orderId, CreatedAt=DateTime.UtcNow, Note="Đối soát hủy đơn PayOS" });
                voucher.OrderId = null;
                voucher.UsedAt = null;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
