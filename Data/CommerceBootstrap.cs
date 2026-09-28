using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Models;
namespace THAN_NONG_SHOP.Data;
public static class CommerceBootstrap
{
    public static void Seed(THAN_NONG_SHOP_DbContext db)
    {
        using var tx=db.Database.BeginTransaction();
        if(!db.Roles.Any(r=>r.Id==3)) {
            db.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Roles ON");
            db.Roles.Add(new Role{Id=3,roleName="Seller"});db.SaveChanges();
            db.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Roles OFF");
        }
        foreach(var p in db.Products.Where(p=>!p.Batches.Any()).ToList())
            db.ProductBatches.Add(new ProductBatch{ProductId=p.Id,Code=$"LEGACY-{p.Id}",RemainingQuantity=p.stockQuantity,IsLegacy=true});
        db.SaveChanges();tx.Commit();
    }
}
