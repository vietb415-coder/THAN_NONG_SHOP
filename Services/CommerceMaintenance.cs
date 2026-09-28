using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Services;

public sealed class CommerceMaintenance(IServiceScopeFactory scopes,ILogger<CommerceMaintenance> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
        do {try {await TickAsync(ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}catch(Exception ex){logger.LogError(ex,"Tác vụ đơn hàng/hạn sử dụng thất bại.");}}
        while(await timer.WaitForNextTickAsync(ct));
    }
    public async Task TickAsync(CancellationToken ct)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<THAN_NONG_SHOP_DbContext>();
        await scope.ServiceProvider.GetRequiredService<InventoryService>().RefreshExpiryAsync(ct);
        var pending=await db.Oders.AsNoTracking().Where(o=>o.Status==OrderStatus.AwaitingPayment).OrderBy(o=>o.OrderDate).Take(100).ToListAsync(ct);
        foreach(var snapshot in pending) {
            var expired=(snapshot.PaymentExpiresAt??snapshot.OrderDate.ToUniversalTime().AddMinutes(15))<=DateTime.UtcNow;
            PaymentProbe probe;
            try{probe=await scope.ServiceProvider.GetRequiredService<PaymentGateways>().QueryAsync(snapshot,ct);}
            catch(Exception ex){logger.LogWarning(ex,"Chưa đối soát được đơn {Id}.",snapshot.Id);probe=new(false,false,0);}
            if(!expired && !probe.Paid)continue;
            using var orderScope=scopes.CreateScope();var orderDb=orderScope.ServiceProvider.GetRequiredService<THAN_NONG_SHOP_DbContext>();
            await using var tx=await orderDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var order=await orderDb.Oders.SingleAsync(o=>o.Id==snapshot.Id,ct);if(order.Status!=OrderStatus.AwaitingPayment)continue;
            var lifecycle=orderScope.ServiceProvider.GetRequiredService<OrderLifecycle>();
            if(probe.Paid){if(!await lifecycle.PaidAsync(order,probe.Amount,ct))order.PaymentNeedsReview=true;}
            else {order.PaymentNeedsReview=!probe.Known;await lifecycle.CancelAsync(order,"Hết hạn chờ thanh toán 15 phút",ct);}
            await orderDb.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        }
    }
}
