using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Services;

public sealed class OrderLifecycle(THAN_NONG_SHOP_DbContext db,InventoryService inventory,EmailDelivery mail)
{
    public async Task CancelAsync(Oder order,string reason,CancellationToken ct)
    {
        if(order.Status==OrderStatus.Cancelled)return;
        await inventory.RestoreAsync(order,ct);
        order.Status=OrderStatus.Cancelled;
        var voucher=await db.PromotionVouchers.FirstOrDefaultAsync(v=>v.OrderId==order.Id,ct);
        if(voucher!=null) {voucher.OrderId=null;voucher.UsedAt=null;db.PromotionVoucherEvents.Add(new PromotionVoucherEvent{VoucherId=voucher.Id,UserName=voucher.UserName,OrderId=order.Id,EventType=VoucherEventTypes.Released,Note=reason,CreatedAt=DateTime.UtcNow});}
    }
    public async Task<bool> PaidAsync(Oder order,decimal amount,CancellationToken ct)
    {
        if(amount!=order.TotalPrice) return false;
        if(order.Status==OrderStatus.Cancelled || order.InventoryRestored) {order.PaymentNeedsReview=true;return true;}
        if(order.Status!=OrderStatus.AwaitingPayment)return true;
        order.Status=OrderStatus.Paid;
        var voucher=await db.PromotionVouchers.FirstOrDefaultAsync(v=>v.OrderId==order.Id,ct);
        if(voucher!=null) {voucher.UsedAt=DateTime.UtcNow;db.PromotionVoucherEvents.Add(new PromotionVoucherEvent{VoucherId=voucher.Id,UserName=voucher.UserName,OrderId=order.Id,EventType=VoucherEventTypes.Used,CreatedAt=DateTime.UtcNow});}
        await QueueOrderEmailAsync(order,ct);
        return true;
    }
    public async Task QueueOrderEmailAsync(Oder order,CancellationToken ct)
    {
        var user=await db.Users.AsNoTracking().FirstOrDefaultAsync(u=>u.UserName==order.UserName,ct);
        if(user!=null) mail.Queue(user.Email,$"Xác nhận đơn hàng #{order.Id}",$"Đơn hàng #{order.Id} đã được ghi nhận.\nTrạng thái: {order.Status}\nTổng thanh toán: {order.TotalPrice:N0} VND\nGiao hàng: {ShippingMethods.Names.GetValueOrDefault(order.ShippingMethod,order.ShippingMethod)}\nĐịa chỉ: {order.Address}");
    }
}
