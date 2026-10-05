using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Services;

// Mutations run inside the caller's serializable order/batch transaction.
public sealed class InventoryService(THAN_NONG_SHOP_DbContext db)
{
    public async Task ReserveAsync(Oder order,List<CartItem> cart,CancellationToken ct)
    {
        foreach(var item in cart.OrderBy(i=>i.Product!.Id)) {
            var p=await db.Products.Include(p=>p.Batches).SingleAsync(p=>p.Id==item.Product!.Id,ct);
            var batches=p.Batches.Where(b=>b.RemainingQuantity>0 && (b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today)).OrderBy(b=>b.ExpiryDate??DateTime.MaxValue).ThenBy(b=>b.Id).ToList();
            if(item.Quantity<=0 || item.Quantity>batches.Sum(b=>b.RemainingQuantity)) throw new InvalidOperationException($"Sản phẩm {p.Name} hiện không đủ số lượng trong kho.");
            // Snapshot prices and seller while stock is locked; reject stale checkout pricing.
            if(p.price!=item.Product!.price) throw new InvalidOperationException("Giá sản phẩm đã thay đổi. Vui lòng kiểm tra lại giỏ hàng.");
            var detail=new OderDetail{Oder=order,ProductId=p.Id,Quantity=item.Quantity,Price=p.price,SellerUserName=p.SellerUserName,FulfillmentStatus=OrderStatus.Pending};
            db.OderDetails.Add(detail);var remaining=item.Quantity;
            foreach(var batch in batches) {
                var take=Math.Min(remaining,batch.RemainingQuantity);if(take==0)break;
                batch.RemainingQuantity-=take;
                detail.Allocations.Add(new BatchAllocation{ProductBatch=batch,Quantity=take});remaining-=take;
            }
            p.stockQuantity=batches.Sum(b=>b.RemainingQuantity);
        }
    }
    public async Task RestoreAsync(Oder order,CancellationToken ct)
    {
        if(order.InventoryRestored)return;
        var details=await db.OderDetails.Include(d=>d.Allocations).ThenInclude(a=>a.ProductBatch).Where(d=>d.OderId==order.Id).ToListAsync(ct);
        foreach(var detail in details) {
            if(detail.Allocations.Count==0) {
                var batch=await db.ProductBatches.FirstOrDefaultAsync(b=>b.ProductId==detail.ProductId && b.IsLegacy,ct);
                if(batch==null) { batch=new ProductBatch{ProductId=detail.ProductId,Code=$"RETURN-{order.Id}-{detail.Id}",IsLegacy=true}; db.ProductBatches.Add(batch); }
                batch.RemainingQuantity+=detail.Quantity;
            } else foreach(var a in detail.Allocations) a.ProductBatch.RemainingQuantity+=a.Quantity;
        }
        // Update stock and the restoration marker together in the caller's transaction.
        // Include newly created legacy batches without an intermediate SaveChanges.
        foreach(var id in details.Select(d=>d.ProductId).Distinct()) {
            var p=await db.Products.Include(p=>p.Batches).SingleAsync(p=>p.Id==id,ct);
            var batches=p.Batches.Concat(db.ProductBatches.Local.Where(b=>b.ProductId==id)).Distinct();
            p.stockQuantity=batches.Where(b=>b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today).Sum(b=>b.RemainingQuantity);
        }
        order.InventoryRestored=true;
    }
    public async Task RefreshExpiryAsync(CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var products=await db.Products.Include(p=>p.Batches).ToListAsync(ct);
        foreach(var p in products) {
            foreach(var b in p.Batches) b.IsNearExpiry=ShopRules.NearExpiry(b.ExpiryDate,ShopRules.Today) && b.RemainingQuantity>0;
            p.stockQuantity=p.Batches.Where(b=>b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today).Sum(b=>b.RemainingQuantity);
        }
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
