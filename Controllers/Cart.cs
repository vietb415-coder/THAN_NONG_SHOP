using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Services;

namespace THAN_NONG_SHOP.Controllers;

public class CartController(THAN_NONG_SHOP_DbContext db,CartState cart,InventoryService inventory,OrderLifecycle lifecycle,PaymentGateways gateways,ILogger<CartController> logger):Controller
{
    private const string PromotionKey="AppliedPromotionCode";
    private string? Username=>User.FindFirstValue(ClaimTypes.NameIdentifier);
    private string Shipping=>HttpContext.Session.GetString("ShippingMethod") ?? "standard";
    private async Task<PromotionResult> PromotionAsync(decimal subtotal,CancellationToken ct)
    {
        var fee=ShippingMethods.Fee(Shipping);var code=HttpContext.Session.GetString(PromotionKey);
        var fallback=PromotionCatalog.Calculate((string?)null,subtotal,fee) with {Message=""};
        if(string.IsNullOrWhiteSpace(code))return fallback;
        var hash=VoucherSecurity.HashCode(code);
        var voucher=await db.PromotionVouchers.AsNoTracking().Include(v=>v.Reward).FirstOrDefaultAsync(v=>v.CodeHash==hash && v.UserName==Username && v.OrderId==null && v.UsedAt==null && v.ExpiresAt>=DateTime.UtcNow,ct);
        if(voucher==null){HttpContext.Session.Remove(PromotionKey);return fallback;}
        var result=voucher.Reward==null?PromotionCatalog.Calculate(voucher.TemplateCode,subtotal,fee):PromotionCatalog.Calculate(voucher.Reward,subtotal,fee);
        if(!result.IsValid)HttpContext.Session.Remove(PromotionKey);
        return result with {Code=code};
    }
    private void Prices(decimal subtotal,PromotionResult p)
    {
        ViewBag.Subtotal=subtotal;ViewBag.Total=p.FinalTotal;ViewBag.ShippingFee=p.ShippingFee;ViewBag.Discount=p.DiscountAmount;
        ViewBag.PromotionCode=p.IsValid?p.Code:"";ViewBag.PromotionMessage=p.IsValid?p.Message:"";
        ViewBag.PromotionGift=p.IsValid && p.DiscountAmount==0?p.Message:"";ViewBag.ShippingMethod=Shipping;
    }
    public async Task<IActionResult> Index(CancellationToken ct)
    {var items=await cart.ProductsAsync(ct);var sum=items.Sum(i=>i.Product!.price*i.Quantity);Prices(sum,await PromotionAsync(sum,ct));return View(items);}
    [HttpGet]
    public async Task<IActionResult> Snapshot(CancellationToken ct)=>Json(new {initialized=Request.Cookies.ContainsKey("CartItems"),authenticated=User.Identity?.IsAuthenticated==true,items=await cart.ReadAsync(ct)});
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreGuest([FromBody]List<CartEntry>? entries,CancellationToken ct)
    {
        if(User.Identity?.IsAuthenticated==true || (await cart.ReadAsync(ct)).Count>0)return NoContent();
        if(entries==null || entries.Count>30)return BadRequest();
        var ids=entries.Select(i=>i.ProductId).ToArray();var products=await db.Products.AsNoTracking().Include(p=>p.Batches).Where(p=>ids.Contains(p.Id)).ToListAsync(ct);
        var stock=products.ToDictionary(p=>p.Id,p=>p.Batches.Where(b=>b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today).Sum(b=>b.RemainingQuantity));
        await cart.WriteAsync(entries.Where(i=>i.Quantity>0 && stock.GetValueOrDefault(i.ProductId)>0).Select(i=>i with {Quantity=Math.Min(i.Quantity,stock[i.ProductId])}),ct);return NoContent();
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int productId,int quantity,CancellationToken ct)
    {
        var product=await db.Products.AsNoTracking().Include(p=>p.Batches).FirstOrDefaultAsync(p=>p.Id==productId,ct);if(product==null)return NotFound();
        var entries=await cart.ReadAsync(ct);var current=entries.FirstOrDefault(i=>i.ProductId==productId);
        var stock=product.Batches.Where(b=>b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today).Sum(b=>b.RemainingQuantity);var wanted=(long)(current?.Quantity??0)+quantity;
        if(quantity<=0 || wanted>999 || wanted>stock || (current==null && entries.Count>=30)){
            TempData["CartError"]=quantity<=0?"Số lượng không hợp lệ.":$"Chỉ còn {stock} sản phẩm trong kho. Giỏ tối đa 30 mặt hàng, 999 sản phẩm mỗi loại.";
            return RedirectToAction("Details","Products",new {id=productId});}
        entries.RemoveAll(i=>i.ProductId==productId);entries.Add(new(productId,(int)wanted));await cart.WriteAsync(entries,ct);return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int productId,int quantity,CancellationToken ct)
    {
        var entries=await cart.ReadAsync(ct);
        if(quantity<0){TempData["CartError"]="Số lượng không được âm.";return RedirectToAction(nameof(Index));}
        if(quantity==0)entries.RemoveAll(i=>i.ProductId==productId);
        else {
            var stock=await db.ProductBatches.Where(b=>b.ProductId==productId && (b.ExpiryDate==null || b.ExpiryDate>=ShopRules.Today)).SumAsync(b=>(int?)b.RemainingQuantity,ct)??0;
            if(quantity>stock || quantity>999){TempData["CartError"]=$"Chỉ còn {stock} sản phẩm trong kho.";return RedirectToAction(nameof(Index));}
            entries=entries.Select(i=>i.ProductId==productId?i with {Quantity=quantity}:i).ToList();}
        await cart.WriteAsync(entries,ct);return RedirectToAction(nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromCart(int productId,CancellationToken ct)
    {var entries=await cart.ReadAsync(ct);entries.RemoveAll(i=>i.ProductId==productId);await cart.WriteAsync(entries,ct);return RedirectToAction(nameof(Index));}
    [HttpPost,ValidateAntiForgeryToken]
    public IActionResult SetShipping(string shippingMethod)
    {if(!ShippingMethods.Names.ContainsKey(shippingMethod??""))return BadRequest();HttpContext.Session.SetString("ShippingMethod",shippingMethod);return RedirectToAction(nameof(Checkout));}
    [Authorize(Roles="User,Seller"),HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyPromotion(string? promotionCode,string? returnTo,CancellationToken ct)
    {
        var code=(promotionCode??"").Trim().ToUpperInvariant();var hash=VoucherSecurity.HashCode(code);var items=await cart.ProductsAsync(ct);var subtotal=items.Sum(i=>i.Product!.price*i.Quantity);
        var voucher=await db.PromotionVouchers.AsNoTracking().Include(v=>v.Reward).FirstOrDefaultAsync(v=>v.CodeHash==hash && v.UserName==Username && v.UsedAt==null && v.OrderId==null && v.ExpiresAt>=DateTime.UtcNow,ct);
        var result=voucher==null?null:voucher.Reward==null?PromotionCatalog.Calculate(voucher.TemplateCode,subtotal,ShippingMethods.Fee(Shipping)):PromotionCatalog.Calculate(voucher.Reward,subtotal,ShippingMethods.Fee(Shipping));
        HttpContext.Session.Remove(PromotionKey);
        if(result?.IsValid!=true)TempData["PromotionError"]=result?.Message??"Mã giảm giá không hợp lệ hoặc đã hết lượt.";
        else {HttpContext.Session.SetString(PromotionKey,code);TempData["PromotionSuccess"]=$"Đã áp dụng voucher: {result.Message}";}
        return RedirectToAction(returnTo=="checkout"?nameof(Checkout):nameof(Index));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public IActionResult RemovePromotion(string? returnTo){HttpContext.Session.Remove(PromotionKey);return RedirectToAction(returnTo=="checkout"?nameof(Checkout):nameof(Index));}
    [Authorize(Roles="User,Seller"),HttpGet]
    public async Task<IActionResult> Checkout(CancellationToken ct)
    {
        var items=await cart.ProductsAsync(ct);if(items.Count==0)return RedirectToAction(nameof(Index));var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserName==Username,ct);
        ViewBag.UserFullName=user.Fullname;ViewBag.UserPhone=user.Phone;var subtotal=items.Sum(i=>i.Product!.price*i.Quantity);Prices(subtotal,await PromotionAsync(subtotal,ct));
        var token=Guid.NewGuid().ToString("N");HttpContext.Session.SetString("CheckoutToken",token);ViewBag.CheckoutToken=token;return View(items);
    }
    [Authorize(Roles="User,Seller"),HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(string customerName,string shippingAddress,string shippingPhone,string paymentMethod,string shippingMethod,string checkoutToken,CancellationToken ct)
    {
        customerName=(customerName??"").Trim();shippingAddress=(shippingAddress??"").Trim();shippingPhone=(shippingPhone??"").Trim();paymentMethod=(paymentMethod??"").ToLowerInvariant();
        if(checkoutToken==null || checkoutToken!=HttpContext.Session.GetString("CheckoutToken")){TempData["CheckoutError"]="Phiên thanh toán đã thay đổi. Vui lòng kiểm tra lại đơn.";return RedirectToAction(nameof(Checkout));}
        if(customerName.Length is <2 or >100 || shippingAddress.Length is <5 or >500 || !Regex.IsMatch(shippingPhone,@"^0[0-9]{9}$")){
            TempData["CheckoutError"]="Vui lòng nhập đủ tên, địa chỉ hợp lệ và số điện thoại 10 chữ số.";return RedirectToAction(nameof(Checkout));}
        if(!ShippingMethods.Names.ContainsKey(shippingMethod??"") || paymentMethod is not ("cod" or "payos" or "momo" or "vnpay"))return BadRequest("Phương thức không hợp lệ.");
        if(!gateways.IsConfigured(paymentMethod)){TempData["CheckoutError"]="Cổng thanh toán chưa được cấu hình. Vui lòng chọn COD hoặc liên hệ cửa hàng.";return RedirectToAction(nameof(Checkout));}
        HttpContext.Session.SetString("ShippingMethod",shippingMethod);var items=await cart.ProductsAsync(ct);if(items.Count==0)return RedirectToAction(nameof(Index));
        Oder order;
        try {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var previous=await db.Oders.FirstOrDefaultAsync(o=>o.CheckoutToken==checkoutToken && o.UserName==Username,ct);if(previous!=null)return RedirectToAction("Index","Orders");
            var subtotal=items.Sum(i=>i.Product!.price*i.Quantity);var p=await PromotionAsync(subtotal,ct);
            if(paymentMethod!="cod" && p.FinalTotal<=0)throw new InvalidOperationException("Đơn 0đ vui lòng chọn COD.");
            order=new Oder {UserName=Username,CustomerName=customerName,Address=shippingAddress,PhoneNumber=shippingPhone,OrderDate=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime,
                Subtotal=subtotal,ShippingFee=p.ShippingFee,DiscountAmount=p.DiscountAmount,TotalPrice=p.FinalTotal,ShippingMethod=shippingMethod,PaymentMethod=paymentMethod,
                Status=paymentMethod=="cod"?OrderStatus.Pending:OrderStatus.AwaitingPayment,PaymentReference=Guid.NewGuid().ToString("N"),
                PaymentExpiresAt=paymentMethod=="cod"?null:DateTime.UtcNow.AddMinutes(15),CheckoutToken=checkoutToken,PayOSOrderCode=paymentMethod=="payos"?DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():null};
            db.Oders.Add(order);await db.SaveChangesAsync(ct);
            if(p.IsValid){
                var hash=VoucherSecurity.HashCode(p.Code);var v=await db.PromotionVouchers.Include(v=>v.Reward).SingleOrDefaultAsync(v=>v.CodeHash==hash && v.UserName==Username && v.UsedAt==null && v.OrderId==null && v.ExpiresAt>=DateTime.UtcNow,ct);
                if(v==null)throw new InvalidOperationException("Voucher vừa hết hiệu lực, vui lòng thử lại.");
                v.OrderId=order.Id;v.UsedAt=paymentMethod=="cod"?DateTime.UtcNow:null;order.PromotionTemplateCode=v.TemplateCode;
                db.PromotionVoucherEvents.Add(new PromotionVoucherEvent{VoucherId=v.Id,EventType=paymentMethod=="cod"?VoucherEventTypes.Used:VoucherEventTypes.Reserved,UserName=Username!,OrderId=order.Id,CreatedAt=DateTime.UtcNow});
                if(v.Reward?.IsGift==true)db.OrderGiftItems.Add(new OrderGiftItem{OrderId=order.Id,PromotionVoucherId=v.Id,Name=v.Reward.GiftName??v.Reward.Title,Quantity=1,UnitPrice=0});}
            await inventory.ReserveAsync(order,items,ct);if(paymentMethod=="cod")await lifecycle.QueueOrderEmailAsync(order,ct);
            await db.SaveChangesAsync(ct);await cart.WriteAsync([],ct);await tx.CommitAsync(ct);
        } catch(Exception ex) when(ex is InvalidOperationException or DbUpdateException or Microsoft.Data.SqlClient.SqlException){
            logger.LogWarning(ex,"Đặt hàng bị từ chối.");TempData["CheckoutError"]=ex is InvalidOperationException?ex.Message:"Dữ liệu giỏ hàng hoặc tồn kho vừa thay đổi. Vui lòng thử lại.";return RedirectToAction(nameof(Checkout));}
        HttpContext.Session.Remove("CheckoutToken");HttpContext.Session.Remove(PromotionKey);
        if(paymentMethod!="cod"){
            try {var url=await gateways.CreateAsync(order,HttpContext.Connection.RemoteIpAddress?.ToString()??"127.0.0.1",ct);order.PaymentUrl=url;await db.SaveChangesAsync(ct);return Redirect(url);}
            catch(Exception ex){logger.LogError(ex,"Không tạo được link cho đơn {Id}.",order.Id);TempData["OrderMessage"]="Chưa tạo được liên kết thanh toán. Đơn được giữ tối đa 15 phút và sẽ tự hủy nếu chưa thanh toán.";return RedirectToAction("Index","Orders");}}
        return RedirectToAction(nameof(OrderSuccess));
    }
    public IActionResult OrderSuccess()=>View();
}
