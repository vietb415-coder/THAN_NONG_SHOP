using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Services;

public sealed record CartEntry(int ProductId,int Quantity);
public sealed class CartState(THAN_NONG_SHOP_DbContext db,IHttpContextAccessor accessor,IDataProtectionProvider protection)
{
    private HttpContext Http => accessor.HttpContext ?? throw new InvalidOperationException("HTTP context required.");
    private readonly IDataProtector protector=protection.CreateProtector("THAN_NONG_SHOP.Cart.v1");
    private string? UserName => Http.User.Identity?.IsAuthenticated==true ? Http.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    public List<CartEntry> Guest()
    {
        try { return Clean(JsonSerializer.Deserialize<List<CartEntry>>(protector.Unprotect(Http.Request.Cookies["CartItems"] ?? "")) ?? []); }
        catch { return []; }
    }
    private static List<CartEntry> Clean(IEnumerable<CartEntry> items) => items.Where(i=>i.ProductId>0 && i.Quantity>0).GroupBy(i=>i.ProductId).Take(30)
        .Select(g=>new CartEntry(g.Key,(int)Math.Min(999,g.Sum(i=>(long)i.Quantity)))).ToList();
    public async Task<List<CartEntry>> ReadAsync(CancellationToken ct=default) => UserName is { } name
        ? await db.SavedCartItems.Where(c=>c.UserName==name).OrderBy(c=>c.Id).Select(c=>new CartEntry(c.ProductId,c.Quantity)).ToListAsync(ct) : Guest();
    public async Task<List<CartItem>> ProductsAsync(CancellationToken ct=default)
    {
        var items=await ReadAsync(ct);var ids=items.Select(i=>i.ProductId).ToArray();
        var products=await db.Products.AsNoTracking().Include(p=>p.Batches).Where(p=>ids.Contains(p.Id)).ToDictionaryAsync(p=>p.Id,ct);
        foreach(var p in products.Values) p.stockQuantity=p.Batches.Where(b=>b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today).Sum(b=>b.RemainingQuantity);
        return items.Where(i=>products.ContainsKey(i.ProductId)).Select(i=>new CartItem {Product=products[i.ProductId],Quantity=i.Quantity}).ToList();
    }
    public async Task WriteAsync(IEnumerable<CartEntry> raw,CancellationToken ct=default)
    {
        var items=Clean(raw);
        if(UserName is { } name) await SaveUserAsync(name,items,ct);
        else Http.Response.Cookies.Append("CartItems",protector.Protect(JsonSerializer.Serialize(items)),new CookieOptions {HttpOnly=true,Secure=Http.Request.IsHttps,SameSite=SameSiteMode.Lax,IsEssential=true,Expires=DateTimeOffset.UtcNow.AddDays(30)});
    }
    private async Task SaveUserAsync(string name,List<CartEntry> entries,CancellationToken ct)
    {
        var existing=await db.SavedCartItems.Where(c=>c.UserName==name).ToListAsync(ct);
        foreach(var old in existing) { var item=entries.FirstOrDefault(e=>e.ProductId==old.ProductId); if(item==null)db.Remove(old);else old.Quantity=item.Quantity; }
        foreach(var item in entries.Where(e=>!existing.Any(x=>x.ProductId==e.ProductId))) db.SavedCartItems.Add(new SavedCartItem{UserName=name,ProductId=item.ProductId,Quantity=item.Quantity});
        await db.SaveChangesAsync(ct);
    }
    public async Task MergeGuestAsync(string name,CancellationToken ct=default)
    {
        var guest=Guest();
        if(guest.Count>0) {
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var existing=await db.SavedCartItems.Where(c=>c.UserName==name).Select(c=>new CartEntry(c.ProductId,c.Quantity)).ToListAsync(ct);
            var combined=Clean(existing.Concat(guest)); var ids=combined.Select(i=>i.ProductId).ToArray();
            var products=await db.Products.AsNoTracking().Include(p=>p.Batches).Where(p=>ids.Contains(p.Id)).ToListAsync(ct);
            var stock=products.ToDictionary(p=>p.Id,p=>p.Batches.Where(b=>b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today).Sum(b=>b.RemainingQuantity));
            await SaveUserAsync(name,combined.Where(i=>stock.GetValueOrDefault(i.ProductId)>0).Select(i=>i with {Quantity=Math.Min(i.Quantity,stock[i.ProductId])}).ToList(),ct);
            await tx.CommitAsync(ct);
        }
        Http.Response.Cookies.Delete("CartItems");Http.Session.Remove("AppliedPromotionCode");
    }
}
