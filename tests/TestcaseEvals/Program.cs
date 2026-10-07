using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Services;
using THAN_NONG_SHOP.Controllers;

var failures=0;
void Check(bool ok,string name){Console.WriteLine($"{(ok?"PASS":"FAIL")} {name}");if(!ok)failures++;}
var opts=new DbContextOptionsBuilder<THAN_NONG_SHOP_DbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
    .ConfigureWarnings(w=>w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
using var db=new THAN_NONG_SHOP_DbContext(opts);
var env=new TestEnvironment();
var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
    ["Email:DeliveryMethod"]="PickupDirectory",["Email:From"]="sender@example.com",
    ["Email:PickupDirectory"]=env.ContentRootPath+"/mail",["Site:PublicBaseUrl"]="https://shop.example.com"
}).Build();
var email=new EmailDelivery(db,config,env);var inventory=new InventoryService(db);var lifecycle=new OrderLifecycle(db,inventory,email);
var http=new DefaultHttpContext{Session=new MemorySession()};
var protect=new EphemeralDataProtectionProvider();
void Setup(Controller controller){controller.ControllerContext=new ControllerContext{HttpContext=http,RouteData=new Microsoft.AspNetCore.Routing.RouteData(),ActionDescriptor=new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()};controller.TempData=new TempDataDictionary(http,new MemoryTempData());controller.Url=new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(controller.ControllerContext);}
var hasher=new Microsoft.AspNetCore.Identity.PasswordHasher<user>();
AccountController Account(){var c=new AccountController(db,hasher,protect,email,new CartState(db,new HttpContextAccessor{HttpContext=http},protect));Setup(c);return c;}
var a=new user{UserName="customer",Email="customer@example.com",Phone="0912345678",Fullname="Customer",RoleId=2,EmailConfirmed=true};
a.Password=hasher.HashPassword(a,"Strong!123");db.Users.Add(a);await db.SaveChangesAsync();
db.Entry(a).Property(nameof(user.NormalizedEmail)).CurrentValue=ShopRules.NormalizeEmail(a.Email);await db.SaveChangesAsync();
var wrong=Account();await wrong.Login("0912345678","bad");
Check(wrong.ModelState.Values.SelectMany(v=>v.Errors).Any(e=>e.ErrorMessage=="Thông tin đăng nhập không chính xác."),"TC_38 invalid password does not issue login");
var unknown=Account();await unknown.Login("missing@example.com","bad");
Check(unknown.ModelState.Values.SelectMany(v=>v.Errors).Any(e=>e.ErrorMessage=="Thông tin đăng nhập không chính xác."),"TC_39 unknown email rejected");
a.IsActive=false;await db.SaveChangesAsync();var locked=Account();await locked.Login(a.Phone,"Strong!123");
Check(locked.ModelState.Values.SelectMany(v=>v.Errors).Any(e=>e.ErrorMessage.Contains("tạm khóa")),"TC_40 phone login finds locked account");
a.IsActive=true;a.EmailConfirmed=false;await db.SaveChangesAsync();var unconfirmed=Account();await unconfirmed.Login("CUSTOMER@EXAMPLE.COM","Strong!123");
Check(unconfirmed.ModelState.Values.SelectMany(v=>v.Errors).Any(e=>e.ErrorMessage.Contains("kích hoạt")),"TC_07 uppercase email resolves account before activation check");
var duplicate=Account();await duplicate.Register("different","Different","CUSTOMER@EXAMPLE.COM","0987654321","Strong!123","Strong!123");
Check(duplicate.ModelState.Values.SelectMany(v=>v.Errors).Any(e=>e.ErrorMessage.Contains("đã được đăng ký")),"TC_37 uppercase duplicate email rejected");
var registration=Account();await registration.Register("new_customer","New Customer","new@example.com","0987654321","Strong!123","Strong!123");
var created=await db.Users.SingleAsync(u=>u.UserName=="new_customer");var confirmation=await db.EmailMessages.SingleAsync();
Check(confirmation.SentAt!=null && Directory.GetFiles(config["Email:PickupDirectory"]!).Length==1,"TC_03 registration attempts confirmation delivery immediately (local pickup)");
var registrationReplay = Account();
var replayResult = await registrationReplay.Register("new_customer", "New Customer", "NEW@EXAMPLE.COM", "0987654321", "Strong!123", "Strong!123");
Check(replayResult is RedirectToActionResult { ActionName: "Login" } && registrationReplay.ModelState.IsValid
    && await db.Users.CountAsync(u=>u.UserName=="new_customer")==1 && await db.EmailMessages.CountAsync()==1,
    "Repeated pending registration acknowledges saved account without another account or email");
var wrongReplay = Account();
Check(await wrongReplay.Register("new_customer", "New Customer", "new@example.com", "0987654321", "Different!123", "Different!123") is ViewResult
    && !wrongReplay.ModelState.IsValid, "Pending registration with a different password remains rejected");
var token=confirmation.Body.Split("&token=")[1].Split('\n')[0];var confirm=Account();await confirm.ConfirmEmail(created.UserName,token);
var confirmedReplay = Account();
Check(await confirmedReplay.Register("new_customer", "New Customer", "new@example.com", "0987654321", "Strong!123", "Strong!123") is ViewResult
    && !confirmedReplay.ModelState.IsValid, "Confirmed account cannot be registered again");
Check(created.EmailConfirmed && created.ConfirmationTokenHash==null,"TC_05 confirmation activates account");
Check(await confirm.ConfirmEmail(created.UserName,token) is BadRequestObjectResult,"Confirmation token cannot be reused");

var loginServices=new Microsoft.Extensions.DependencyInjection.ServiceCollection();
Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Microsoft.AspNetCore.Authentication.IAuthenticationService,TestAuthentication>(loginServices);
http.RequestServices=Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(loginServices);
a.EmailConfirmed=true;await db.SaveChangesAsync();
foreach(var role in new[]{(2,false,"User"),(3,true,"Seller"),(1,false,"Admin")}){
 a.RoleId=role.Item1;a.SellerApproved=role.Item2;await db.SaveChangesAsync();
 var login=Account();var result=await login.Login(a.Phone,"Strong!123");
 Check(result is RedirectToActionResult && http.User.IsInRole(role.Item3),$"TC_05/07/08 successful phone login role {role.Item3}");
}
http.User=new ClaimsPrincipal(new ClaimsIdentity());
var today=ShopRules.Today;
Check(!ShopRules.ValidBatch("B",today,today,5),"TC_41 equal harvest and expiry rejected");
Check(!ShopRules.ValidBatch("B",today,today.AddDays(-1),5),"TC_41 expiry before harvest rejected");
Check(ShopRules.NearExpiry(today.AddDays(2),today) && !ShopRules.NearExpiry(today.AddDays(3),today),"TC_44/65 exact 3 days excluded, 2 days included");
foreach(var pair in new[]{("day",1),("week",7),("month",31),("quarter",92)}){
 var (start,end)=ShopRules.Period(pair.Item1,new DateTime(2026,10,5));Check((end-start).Days==pair.Item2,$"Report period {pair.Item1}");}
Check(ShippingMethods.Fee("standard")==30000 && ShippingMethods.Fee("express")==50000 && ShippingMethods.Fee("cold")==70000,"TC_70/71/72 shipping fees");
db.Categories.Add(new Category{Id=1,Name="Vegetables",Description="Fresh vegetables"});await db.SaveChangesAsync();
var product=new Product{Name="Fresh tomato",Description="fresh produce",price=100000,categoryId=1,stockQuantity=5};
product.Batches.Add(new ProductBatch{Code="B-1",ExpiryDate=today.AddDays(2),HarvestDate=today,RemainingQuantity=5});db.Products.Add(product);await db.SaveChangesAsync();
var cart=new CartState(db,new HttpContextAccessor{HttpContext=http},protect);
var controller=new CartController(db,cart,inventory,lifecycle,new PaymentGateways(config,new Clients()),NullLogger<CartController>.Instance);Setup(controller);
await controller.Checkout(CancellationToken.None);
Check(controller.TempData["CartError"]?.ToString().Contains("rỗng")==true,"TC_73 empty checkout rejected with message");
http.Session.SetString("CheckoutToken","guest-checkout");
var cookie=protect.CreateProtector("THAN_NONG_SHOP.Cart.v1").Protect("[{\"ProductId\":"+product.Id+",\"Quantity\":2}]");
http.Request.Headers.Cookie="CartItems="+cookie;
await controller.Checkout("Guest Customer","123 Main Street","0912345678","cod","express","guest-checkout",CancellationToken.None);
var order=await db.Oders.SingleAsync();
Check(order.UserName==null && order.Status==OrderStatus.Pending && order.TotalPrice==250000,"TC_69/74 guest COD creates correct order");
Check(product.stockQuantity==3 && product.Batches.Single().RemainingQuantity==3,"TC_75 reservation reduces batch and product stock");
Check(GuestOrderAccess.Contains(http,order.Id),"Guest can access only order ids granted to their session");
var stranger=new DefaultHttpContext{Session=new MemorySession()};Check(!GuestOrderAccess.Contains(stranger,order.Id),"Guest order cannot be read from another session");
await lifecycle.CancelAsync(order,"Test cancellation",CancellationToken.None);await db.SaveChangesAsync();
Check(order.InventoryRestored && product.stockQuantity==5 && product.Batches.Single().RemainingQuantity==5,"Cancellation restores exact stock");
await lifecycle.CancelAsync(order,"Repeated",CancellationToken.None);await db.SaveChangesAsync();Check(product.stockQuantity==5,"Repeated cancellation never restores twice");
var excessive=new Oder{CustomerName="Test",PhoneNumber="0912345678",Address="123 Main Street"};
try{await inventory.ReserveAsync(excessive,[new CartItem{Product=product,Quantity=6}],CancellationToken.None);Check(false,"TC_45 overstock rejected");}
catch(InvalidOperationException){Check(product.stockQuantity==5,"TC_45 overstock rejected without reducing inventory");}

http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,a.UserName),new Claim(ClaimTypes.Role,"User")],"test"));
var completed=new Oder{CustomerName="Customer",PhoneNumber=a.Phone,Address="123 Main Street",Status=OrderStatus.Completed,OrderDate=DateTime.Now};
db.Entry(completed).Property(nameof(Oder.UserName)).CurrentValue=a.UserName;db.Oders.Add(completed);
db.OderDetails.Add(new OderDetail{Oder=completed,ProductId=product.Id,Quantity=1,Price=product.price});await db.SaveChangesAsync();
ProductReviewsController Review(){var c=new ProductReviewsController(db,env);Setup(c);return c;}
await Review().Save(product.Id,0,"Good product",null,CancellationToken.None);Check(!await db.ProductReviews.AnyAsync(),"No stars never saves a review");
foreach(var rating in new[]{1,5}){await Review().Save(product.Id,rating,"Good product",null,CancellationToken.None);Check((await db.ProductReviews.SingleAsync()).Rating==rating,$"TC_58/59 rating {rating} saved");}
FormFile File(string name,byte[] bytes,string mime)=>new(new MemoryStream(bytes),0,bytes.Length,"media",name){Headers=new HeaderDictionary(),ContentType=mime};
FormFile Image()=>File("test.png",[137,80,78,71,13,10,26,10,0],"image/png");
FormFile Video()=>File("test.mp4",[0,0,0,12,102,116,121,112,0,0,0,0],"video/mp4");
await Review().Save(product.Id,5,"Three images",[Image(),Image(),Image()],CancellationToken.None);Check(await db.ReviewMedia.CountAsync()==3,"TC_60 three images accepted");
await Review().Save(product.Id,5,"Four images",[Image(),Image(),Image(),Image()],CancellationToken.None);Check(await db.ReviewMedia.CountAsync()==3,"TC_61 four images rejected without replacing old media");
await Review().Save(product.Id,5,"One video",[Video()],CancellationToken.None);Check(await db.ReviewMedia.CountAsync(m=>m.IsVideo)==1,"TC_62 one video accepted");
await Review().Save(product.Id,5,"Two videos",[Video(),Video()],CancellationToken.None);Check(await db.ReviewMedia.CountAsync()==1,"TC_63 two videos rejected");
Check(ReviewUploads.Inspect(File("bad.png",[1,2,3],"image/png"))==null && ReviewUploads.Inspect(File("evil.exe",[1],"application/octet-stream"))==null,"TC_64 invalid signatures and executable files rejected");
// Reproduce workbook failures against controller behavior, before changing production code.
http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,a.UserName),new Claim(ClaimTypes.Role,"User")],"test"));
async Task<Oder> ReservedOrder(string status,string method="cod") {
 var o=new Oder{CustomerName="Customer",PhoneNumber=a.Phone,Address="123 Main Street",Status=status,PaymentMethod=method,TotalPrice=product.price};
 db.Entry(o).Property(nameof(Oder.UserName)).CurrentValue=a.UserName;db.Oders.Add(o);await db.SaveChangesAsync();
 await inventory.ReserveAsync(o,[new CartItem{Product=product,Quantity=1}],CancellationToken.None);await db.SaveChangesAsync();return o;
}
OrdersController Orders(){var c=new OrdersController(db,lifecycle);Setup(c);return c;}
var packing=await ReservedOrder(OrderStatus.Packing);
await Orders().Cancel(packing.Id,CancellationToken.None);
Check(packing.Status==OrderStatus.Cancelled && packing.InventoryRestored && product.stockQuantity==5,"TC_78 customer cancels packing order and restores stock");
await Orders().Cancel(packing.Id,CancellationToken.None);
Check(product.stockQuantity==(packing.InventoryRestored?5:4),"TC_78 repeated cancellation does not restore twice");
if(!packing.InventoryRestored){await lifecycle.CancelAsync(packing,"Test cleanup",CancellationToken.None);await db.SaveChangesAsync();}
var mixed=await ReservedOrder(OrderStatus.Packing);
(await db.OderDetails.SingleAsync(d=>d.OderId==mixed.Id)).FulfillmentStatus=OrderStatus.Shipping;await db.SaveChangesAsync();
await Orders().Cancel(mixed.Id,CancellationToken.None);
Check(mixed.Status==OrderStatus.Packing && !mixed.InventoryRestored,"TC_78 mixed order with shipped line cannot be cancelled");
await lifecycle.CancelAsync(mixed,"Test cleanup",CancellationToken.None);await db.SaveChangesAsync();
var onlinePacking=await ReservedOrder(OrderStatus.Packing,"vnpay");
await Orders().Cancel(onlinePacking.Id,CancellationToken.None);
Check(onlinePacking.Status==OrderStatus.Cancelled && onlinePacking.PaymentNeedsReview,"TC_78 cancellation of paid online order flags refund review");
if(!onlinePacking.InventoryRestored){await lifecycle.CancelAsync(onlinePacking,"Test cleanup",CancellationToken.None);await db.SaveChangesAsync();}
var someoneElse=await ReservedOrder(OrderStatus.Pending);
db.Entry(someoneElse).Property(nameof(Oder.UserName)).CurrentValue=created.UserName;await db.SaveChangesAsync();
Check(await Orders().Cancel(someoneElse.Id,CancellationToken.None) is NotFoundResult && !someoneElse.InventoryRestored,"TC_78 cannot cancel another customer's order");
await lifecycle.CancelAsync(someoneElse,"Test cleanup",CancellationToken.None);await db.SaveChangesAsync();

ProductsController Products(){var c=new ProductsController(db);Setup(c);return c;}
async Task<List<Product>> Search(string? keyword,int? category=null,decimal? min=null,decimal? max=null){var result=(ViewResult)await Products().Index(keyword,category,min,max,CancellationToken.None);return (List<Product>)result.Model!;}
Check((await Search("  tomato  ")).Count==1,"TC_19/47/79 trimmed product name search");
Check((await Search("fresh produce")).Count==1,"TC_21 product description search");
Check((await Search("tomato",1,90000,110000)).Count==1 && (await Search("tomato",2,90000,110000)).Count==0,"TC_22/46/81/82/83 combined category and price filters");
Check((await Search("tomato",null,110000,90000)).Count==1,"TC_85 reversed price bounds are normalized");
Check((await Search(null,null,-1,null)).Count==1,"TC_86 negative price does not crash");
Check((await Search("does-not-exist")).Count==0,"TC_20/80 unknown search returns empty results");
Check((await Search(null)).Count==1,"TC_23/84 clearing filters restores full catalogue");
http.Request.Headers["X-Requested-With"]="XMLHttpRequest";
Check(await Products().Index("tomato",null,null,null,CancellationToken.None) is PartialViewResult,"TC_24 AJAX returns results without full page layout");
http.Request.Headers.Remove("X-Requested-With");

await cart.WriteAsync([],CancellationToken.None);
await controller.AddToCart(product.Id,2,CancellationToken.None);
Check((await cart.ReadAsync()).Single().Quantity==2 && await db.SavedCartItems.AnyAsync(c=>c.UserName==a.UserName),"TC_25/30/87 customer cart persists in database");
await controller.UpdateQuantity(product.Id,-1,CancellationToken.None);
Check((await cart.ReadAsync()).Single().Quantity==2,"TC_28 negative quantity leaves cart unchanged");
await controller.UpdateQuantity(product.Id,6,CancellationToken.None);
Check((await cart.ReadAsync()).Single().Quantity==2,"TC_48/95 overstock quantity leaves cart unchanged");
await controller.UpdateQuantity(product.Id,3,CancellationToken.None);
Check((await cart.ReadAsync()).Single().Quantity==3,"TC_26/89 cart quantity updates");
await controller.UpdateQuantity(product.Id,0,CancellationToken.None);
Check((await cart.ReadAsync()).Count==0,"TC_49/89 zero quantity removes item");
await controller.AddToCart(product.Id,1,CancellationToken.None);await controller.RemoveFromCart(product.Id,CancellationToken.None);
Check((await cart.ReadAsync()).Count==0,"TC_27/90 removal empties cart");
await controller.AddToCart(product.Id,1,CancellationToken.None);
http.Session.SetString("CheckoutToken","invalid-details");var countBefore=await db.Oders.CountAsync();
await controller.Checkout("","","","cod","standard","invalid-details",CancellationToken.None);
Check(await db.Oders.CountAsync()==countBefore && (await cart.ReadAsync()).Count==1,"TC_92 blank checkout rejected without losing cart");
await controller.ApplyPromotion("MISSING","checkout",CancellationToken.None);
Check(http.Session.GetString("AppliedPromotionCode")==null && controller.TempData["PromotionError"]!=null,"TC_50 invalid voucher rejected with message");
product.Batches.Single().RemainingQuantity=0;product.stockQuantity=0;await db.SaveChangesAsync();
var detailsBefore=await db.OderDetails.CountAsync();
try { await inventory.ReserveAsync(new Oder{CustomerName="Customer",PhoneNumber=a.Phone,Address="123 Main Street"},[new CartItem{Product=product,Quantity=1}],CancellationToken.None);Check(false,"TC_94 checkout sees updated out-of-stock batch"); }
catch(InvalidOperationException) { Check(await db.OderDetails.CountAsync()==detailsBefore && product.stockQuantity==0,"TC_94 stale cart cannot reserve stock after batch sells out"); }
product.Batches.Single().RemainingQuantity=5;product.stockQuantity=5;await db.SaveChangesAsync();

// Authentication failures in the workbook blocked these cases: verify validation independently.
http.User=new ClaimsPrincipal(new ClaimsIdentity());
foreach(var invalid in new[]{("weak","new2@example.com","0981234567","weak","weak","password"),("mismatch","new2@example.com","0981234567","Strong!123","Other!123","confirmPassword"),("bademail","bad-email","0981234567","Strong!123","Strong!123","email"),("badphone","new2@example.com","123","Strong!123","Strong!123","phoneNumber")}) {
 var c=Account();var before=await db.Users.CountAsync();await c.Register(invalid.Item1,"New Customer",invalid.Item2,invalid.Item3,invalid.Item4,invalid.Item5);
 Check(c.ModelState[invalid.Item6]?.Errors.Count>0 && await db.Users.CountAsync()==before,$"TC_32/33/34/35 invalid registration {invalid.Item1} blocked");
}
http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,a.UserName)],"test"));
var already=Account();var usersBefore=await db.Users.CountAsync();
Check(already.Register() is RedirectToActionResult,"TC_04 authenticated visitor cannot open guest registration");
await already.Register("blocked","Blocked","blocked@example.com","0981234567","Strong!123","Strong!123");
Check(await db.Users.CountAsync()==usersBefore,"TC_04 authenticated visitor cannot submit guest registration");
http.User=new ClaimsPrincipal(new ClaimsIdentity());
var duplicatePhone=Account();await duplicatePhone.Register("phone_duplicate","Customer","different@example.com",a.Phone,"Strong!123","Strong!123");
Check(duplicatePhone.ModelState.Values.SelectMany(v=>v.Errors).Any(e=>e.ErrorMessage.Contains("đã được đăng ký")),"TC_31 duplicate phone rejected");
var trimmed=Account();await trimmed.Register("  trim_user  ","  Trim User  ","  trimmed@example.com  ","  0981234567  ","Strong!123","Strong!123");
var trimmedUser=await db.Users.SingleAsync(u=>u.UserName=="trim_user");
Check(trimmedUser.Fullname=="Trim User" && trimmedUser.Email=="trimmed@example.com" && trimmedUser.Phone=="0981234567","TC_36 registration trims name, email and phone consistently");
http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,a.UserName),new Claim(ClaimTypes.Role,"User")],"test"));

var paymentConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
 ["VNPay:TmnCode"]="TESTMERCHANT",["VNPay:HashSecret"]="test-secret-not-real",["Site:PublicBaseUrl"]="https://shop.example.com"
}).Build();
var payments=new PaymentGateways(paymentConfig,new Clients());
var payable=await ReservedOrder(OrderStatus.AwaitingPayment,"vnpay");
payable.PaymentReference="test-payment";payable.PaymentExpiresAt=DateTime.UtcNow.AddMinutes(15);await db.SaveChangesAsync();
var paymentUrl=await payments.CreateAsync(payable,"127.0.0.1",CancellationToken.None);
Check(paymentUrl.Contains("vnp_Amount=10000000") && paymentUrl.Contains("vnp_SecureHash="),"VNPay link uses VND amount multiplied by 100 and signature");
async Task Notify(string responseCode,string transactionStatus,decimal callbackAmount,bool tamper=false){
 var values=new Dictionary<string,string>{["vnp_TmnCode"]="TESTMERCHANT",["vnp_TxnRef"]="test-payment",["vnp_Amount"]=(callbackAmount*100).ToString(System.Globalization.CultureInfo.InvariantCulture),["vnp_ResponseCode"]=responseCode,["vnp_TransactionStatus"]=transactionStatus};
 values["vnp_SecureHash"]=PaymentGateways.Hmac(PaymentGateways.VnpData(values),"test-secret-not-real",true);
 if(tamper)values["vnp_Amount"]="1";
 http.Request.QueryString=QueryString.Create(values);
 var c=new PaymentController(db,payments,lifecycle,NullLogger<PaymentController>.Instance);Setup(c);await c.VNPayIpn(CancellationToken.None);
}
await Notify("24","02",payable.TotalPrice);
Check(payable.Status==OrderStatus.AwaitingPayment && !payable.InventoryRestored && product.stockQuantity==4,"TC_18/52 failed payment keeps reservation until deadline");
await Notify("00","00",payable.TotalPrice,true);
Check(payable.Status==OrderStatus.AwaitingPayment,"VNPay rejects tampered callback signature");
await Notify("00","00",payable.TotalPrice+1);
Check(payable.Status==OrderStatus.AwaitingPayment,"VNPay rejects correctly signed callback with wrong amount");
await Notify("00","00",payable.TotalPrice);
Check(payable.Status==OrderStatus.Paid && product.stockQuantity==4,"TC_17 payment success marks paid without subtracting stock twice");
var emailCount=await db.EmailMessages.CountAsync();await Notify("00","00",payable.TotalPrice);
Check(product.stockQuantity==4 && await db.EmailMessages.CountAsync()==emailCount,"Duplicate payment callback does not repeat inventory or email side effects");
http.Request.QueryString=QueryString.Empty;
await lifecycle.CancelAsync(payable,"Test cleanup",CancellationToken.None);await db.SaveChangesAsync();
var due=await ReservedOrder(OrderStatus.AwaitingPayment,"vnpay");due.PaymentExpiresAt=DateTime.UtcNow.AddSeconds(-1);
var future=await ReservedOrder(OrderStatus.AwaitingPayment,"vnpay");future.PaymentExpiresAt=DateTime.UtcNow.AddMinutes(15);await db.SaveChangesAsync();
var maintenanceServices=new ServiceCollection();
maintenanceServices.AddScoped(_=>new THAN_NONG_SHOP_DbContext(opts));maintenanceServices.AddScoped<InventoryService>();
maintenanceServices.AddScoped<EmailDelivery>(_=>new EmailDelivery(_.GetRequiredService<THAN_NONG_SHOP_DbContext>(),config,env));
maintenanceServices.AddScoped<OrderLifecycle>();maintenanceServices.AddScoped<PaymentGateways>(_=>new PaymentGateways(config,new Clients()));
using(var maintenanceProvider=maintenanceServices.BuildServiceProvider()){
 var worker=new CommerceMaintenance(maintenanceProvider.GetRequiredService<IServiceScopeFactory>(),NullLogger<CommerceMaintenance>.Instance);
 await worker.TickAsync(CancellationToken.None);
 db.ChangeTracker.Clear();
 var expired=await db.Oders.SingleAsync(o=>o.Id==due.Id);
 Check(expired.Status==OrderStatus.Cancelled && expired.InventoryRestored && (await db.Products.SingleAsync(p=>p.Id==product.Id)).stockQuantity==4,"TC_53 maintenance cancels overdue order and restores exact stock");
 Check((await db.Oders.SingleAsync(o=>o.Id==future.Id)).Status==OrderStatus.AwaitingPayment,"TC_52 maintenance preserves order before 15-minute deadline");
 await worker.TickAsync(CancellationToken.None);db.ChangeTracker.Clear();
 Check((await db.Products.SingleAsync(p=>p.Id==product.Id)).stockQuantity==4,"TC_53 repeated maintenance never restores stock twice");
}
// Regression cases from the workbook added on 07/10/2026.
product=await db.Products.Include(p=>p.Batches).SingleAsync(p=>p.Id==product.Id);
a=await db.Users.SingleAsync(u=>u.UserName==a.UserName);
http.User=new ClaimsPrincipal(new ClaimsIdentity());
a.Password="Legacy!123";await db.SaveChangesAsync();
await Account().Login(a.Phone,"wrong");
Check(a.Password=="Legacy!123","WB_2 wrong password leaves legacy password unchanged");
await Account().Login(a.Phone,"Legacy!123");
Check(a.Password.StartsWith("AQAAAA") && http.User.Identity?.IsAuthenticated==true,"WB_1 legacy password upgraded on successful login");
http.User=new ClaimsPrincipal(new ClaimsIdentity());
var oldHasher=new Microsoft.AspNetCore.Identity.PasswordHasher<user>(Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Identity.PasswordHasherOptions{IterationCount=10000}));
a.Password=oldHasher.HashPassword(a,"Legacy!123");var oldHash=a.Password;await db.SaveChangesAsync();
await Account().Login(a.Phone,"Legacy!123");
Check(a.Password!=oldHash && hasher.VerifyHashedPassword(a,a.Password,"Legacy!123")!=Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed,"WB_3 old iteration hash rehashed after correct password");
var ambiguous=new user{UserName=a.Phone,Email="ambiguous@example.com",Phone="0999999999",RoleId=2,EmailConfirmed=true,Fullname="Ambiguous",Password=hasher.HashPassword(a,"Legacy!123")};
db.Users.Add(ambiguous);await db.SaveChangesAsync();http.User=new ClaimsPrincipal(new ClaimsIdentity());
var ambiguousLogin=Account();await ambiguousLogin.Login(a.Phone,"Legacy!123");
Check(http.User.Identity?.IsAuthenticated!=true && !ambiguousLogin.ModelState.IsValid,"WB_4 ambiguous identifiers cannot sign in");
db.Remove(ambiguous);await db.SaveChangesAsync();
await Account().Login(a.Phone,"Legacy!123",false,"https://evil.example/steal");
Check(http.User.Identity?.IsAuthenticated==true,"WB_8 external returnUrl still permits legitimate login");
var expiredAccount=new user{UserName="expired_token",Fullname="Expired Token",Email="expired@example.com",Phone="0901111222",RoleId=2,ConfirmationTokenHash=EmailDelivery.Hash(new string('A',64)),ConfirmationExpiresAt=DateTime.UtcNow.AddSeconds(-1)};
db.Add(expiredAccount);await db.SaveChangesAsync();
Check(await Account().ConfirmEmail(expiredAccount.UserName,new string('A',64)) is BadRequestObjectResult && !expiredAccount.EmailConfirmed,"WB_64 expired confirmation cannot activate account");
var denied=Account();denied.AccessDenied();Check(http.Response.StatusCode==403,"WB_11 access denied renders HTTP 403");http.Response.StatusCode=200;
Check(OrderStatus.CanMove(OrderStatus.Pending,OrderStatus.Packing) && !OrderStatus.CanMove(OrderStatus.Pending,OrderStatus.Shipping)
 && OrderStatus.CanMove(OrderStatus.Paid,OrderStatus.Cancelled) && !OrderStatus.CanMove(OrderStatus.AwaitingPayment,OrderStatus.Packing)
 && !OrderStatus.CanMove(OrderStatus.Completed,OrderStatus.Pending),"WB_42 order transition matrix");
await cart.WriteAsync([new(product.Id,1)],CancellationToken.None);
var ordersBefore=await db.Oders.CountAsync();var stockBefore=product.stockQuantity;
http.Session.SetString("CheckoutToken","expired-voucher");http.Session.SetString("AppliedPromotionCode","NO-LONGER-VALID");
await controller.Checkout("Customer","123 Main Street",a.Phone,"cod","standard","expired-voucher",CancellationToken.None);
Check(await db.Oders.CountAsync()==ordersBefore && product.stockQuantity==stockBefore
 && controller.TempData["CheckoutError"]?.ToString().Contains("Voucher vừa hết hiệu lực")==true,"WB_26 expired voucher blocks checkout before order/stock mutation");
http.Session.Remove("AppliedPromotionCode");http.Session.SetString("CheckoutToken","replay-order");
await controller.Checkout("Customer","123 Main Street",a.Phone,"cod","standard","replay-order",CancellationToken.None);
var committed=await db.Oders.SingleAsync(o=>o.CheckoutToken=="replay-order");var countAfter=await db.Oders.CountAsync();var stockAfter=product.stockQuantity;
var replay=await controller.Checkout("Customer","123 Main Street",a.Phone,"cod","standard","replay-order",CancellationToken.None);
Check(replay is RedirectToActionResult {ControllerName:"Orders"} && await db.Oders.CountAsync()==countAfter && product.stockQuantity==stockAfter,"WB_21 replay after session token cleared returns existing order without another reservation");
http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"stranger")],"test"));
Check(await controller.Checkout("Stranger","123 Main Street",a.Phone,"cod","standard","replay-order",CancellationToken.None) is NotFoundResult,"WB_21 replay token cannot access another customer's order");
http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,a.UserName),new Claim(ClaimTypes.Role,"User")],"test"));
await lifecycle.CancelAsync(committed,"Cleanup",CancellationToken.None);await db.SaveChangesAsync();
await cart.WriteAsync([new(product.Id,1)],CancellationToken.None);
http.Session.SetString("CheckoutToken","bad-token");
await controller.Checkout("Customer","123 Main Street",a.Phone,"cod","standard","wrong-token",CancellationToken.None);
Check(controller.TempData["CheckoutError"]?.ToString().Contains("Phiên thanh toán")==true,"WB_20 wrong checkout token is rejected");
Check(await controller.Checkout("Customer","123 Main Street",a.Phone,"momo","standard","bad-token",CancellationToken.None) is BadRequestObjectResult,"Removed payment method is rejected");
await controller.Checkout("Customer","123 Main Street",a.Phone,"payos","standard","bad-token",CancellationToken.None);
Check(controller.TempData["CheckoutError"]?.ToString().Contains("chưa được cấu hình")==true,"WB_24 missing PayOS keys refuse order before reserve");
foreach(var q in new[]{0,-1}){
 await controller.AddToCart(product.Id,q,CancellationToken.None);
 Check((await cart.ReadAsync()).Single().Quantity==1,$"WB_13 invalid quantity {q} does not mutate cart");
}
http.User=new ClaimsPrincipal(new ClaimsIdentity());http.Request.Headers.Cookie="CartItems=corrupted-cookie";
Check(cart.Guest().Count==0,"WB_17 tampered cookie yields empty cart without exception");
http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,a.UserName),new Claim(ClaimTypes.Role,"User")],"test"));

// Media boundaries and review ownership from white-box cases 48-52.
Check(ReviewUploads.Inspect(File("renamed.jpg",[137,80,78,71,13,10,26,10,0],"image/jpeg"))==null,"WB_50 PNG renamed as JPEG is rejected");
Check(ReviewUploads.Inspect(File("test.png",[137,80,78,71,13,10,26,10,0],"image/jpeg"))==null,"WB_50 mismatched MIME type rejected");
byte[] Sized(byte[] header,int size){var bytes=new byte[size];header.CopyTo(bytes,0);return bytes;}
Check(ReviewUploads.Inspect(File("limit.png",Sized([137,80,78,71,13,10,26,10],5*1024*1024),"image/png"))!=null
 && ReviewUploads.Inspect(File("limit.png",Sized([137,80,78,71,13,10,26,10],5*1024*1024+1),"image/png"))==null,"WB_51 image limit exactly 5 MiB and +1 byte");
Check(ReviewUploads.Inspect(File("limit.mp4",Sized([0,0,0,12,102,116,121,112,0,0,0,0],20*1024*1024),"video/mp4"))!=null
 && ReviewUploads.Inspect(File("limit.mp4",Sized([0,0,0,12,102,116,121,112,0,0,0,0],20*1024*1024+1),"video/mp4"))==null,"WB_51 video limit exactly 20 MiB and +1 byte");
var savedReview=await db.ProductReviews.Include(r=>r.Media).SingleAsync();var oldMedia=savedReview.Media.Select(m=>Path.Combine(env.ContentRootPath,"App_Data","review-media",m.FileName)).ToArray();
await Review().Save(product.Id,1,"New image",[Image()],CancellationToken.None);
Check(oldMedia.All(path=>!System.IO.File.Exists(path)) && savedReview.Media.Count==1 && System.IO.File.Exists(Path.Combine(env.ContentRootPath,"App_Data","review-media",savedReview.Media.Single().FileName)),"WB_52 saving replacement media removes old files after commit");
foreach(var input in new[]{(0,"Valid comment"),(6,"Valid comment"),(1,"ab"),(5,new string('x',1001))}){
 var prev=savedReview.Comment;await Review().Save(product.Id,input.Item1,input.Item2,null,CancellationToken.None);
 Check(savedReview.Comment==prev,$"WB_49 invalid rating/comment {input.Item1}/{input.Item2.Length} leaves review unchanged");
}
foreach(var length in new[]{3,1000}){await Review().Save(product.Id,5,new string('x',length),null,CancellationToken.None);Check(savedReview.Comment.Length==length,$"WB_49 valid comment boundary {length}");}
http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"never-purchased")],"test"));
await Review().Save(product.Id,5,"Not purchased",null,CancellationToken.None);
Check(!await db.ProductReviews.AnyAsync(r=>r.UserName=="never-purchased"),"WB_48 customer without completed purchase cannot review");
http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,a.UserName),new Claim(ClaimTypes.Role,"User")],"test"));
var chat=new ChatbotService(new Clients(),db,new HttpContextAccessor{HttpContext=http},Microsoft.Extensions.Options.Options.Create(new ChatbotOptions()),NullLogger<ChatbotService>.Instance);
http.Request.Headers.Cookie="THAN_NONG_CHAT_VISITOR="+new string('a',32);
var customerConversation=await chat.ReplyAsync(new ChatRequest{Message="đơn hàng của tôi"},CancellationToken.None);
http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"other-customer")],"test"));
var otherConversation=await chat.ReplyAsync(new ChatRequest{Message="xin chào",ConversationId=customerConversation.ConversationId},CancellationToken.None);
Check(otherConversation.ConversationId!=customerConversation.ConversationId,"WB_54 another customer cannot reuse conversation id");
Check(!await chat.SetFeedbackAsync(new ChatFeedbackRequest{MessageId=customerConversation.MessageId,Helpful=true},CancellationToken.None),"WB_57 another customer's feedback target rejected");
http.User=new ClaimsPrincipal(new ClaimsIdentity());
var guestConversation=await chat.ReplyAsync(new ChatRequest{Message="đơn hàng của tôi",ConversationId=customerConversation.ConversationId},CancellationToken.None);
Check(guestConversation.ConversationId!=customerConversation.ConversationId && guestConversation.Orders.Count==0,"WB_55 guest after logout cannot recover customer conversation or orders using same visitor cookie");
Check(!await chat.SetFeedbackAsync(new ChatFeedbackRequest{MessageId=customerConversation.MessageId,Helpful=true},CancellationToken.None),"WB_57 same browser after logout cannot modify customer feedback");
var chatController=new ChatController(chat);
foreach(var length in new[]{0,1001}){
 var response=await chatController.Send(new ChatRequest{Message=new string('x',length)},CancellationToken.None);
 Check(response.Result is BadRequestObjectResult,$"WB_56 invalid chat message length {length}");
}
var validChat=await chatController.Send(new ChatRequest{Message=new string('x',1000)},CancellationToken.None);
Check(validChat.Result is OkObjectResult,"WB_56 1000-character chat message accepted");
Directory.Delete(env.ContentRootPath,true);
Console.WriteLine($"Failures: {failures}. InMemory tests do not verify SQL Server locking or real payment/SMTP providers.");return failures==0?0:1;

sealed class Clients:IHttpClientFactory{public HttpClient CreateClient(string name)=>new();}
sealed class MemorySession:ISession{
 readonly Dictionary<string,byte[]> values=[];public bool IsAvailable=>true;public string Id=>"test";public IEnumerable<string> Keys=>values.Keys;
 public void Clear()=>values.Clear();public Task CommitAsync(CancellationToken ct=default)=>Task.CompletedTask;public Task LoadAsync(CancellationToken ct=default)=>Task.CompletedTask;
 public void Remove(string key)=>values.Remove(key);public void Set(string key,byte[] value)=>values[key]=value;public bool TryGetValue(string key,out byte[]? value)=>values.TryGetValue(key,out value);
}
sealed class MemoryTempData:ITempDataProvider{public IDictionary<string,object> LoadTempData(HttpContext h)=>new Dictionary<string,object>();public void SaveTempData(HttpContext h,IDictionary<string,object> v){}}
sealed class TestEnvironment:IWebHostEnvironment{
 public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="TestcaseEvals";
 public string ContentRootPath{get;set;}=Path.Combine(Path.GetTempPath(),"thannong-cases-"+Guid.NewGuid());public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
 public string WebRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
}

sealed class TestAuthentication:Microsoft.AspNetCore.Authentication.IAuthenticationService{
 public Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> AuthenticateAsync(HttpContext h,string? s)=>Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
 public Task ChallengeAsync(HttpContext h,string? s,Microsoft.AspNetCore.Authentication.AuthenticationProperties? p)=>Task.CompletedTask;
 public Task ForbidAsync(HttpContext h,string? s,Microsoft.AspNetCore.Authentication.AuthenticationProperties? p)=>Task.CompletedTask;
 public Task SignInAsync(HttpContext h,string? s,ClaimsPrincipal principal,Microsoft.AspNetCore.Authentication.AuthenticationProperties? p){h.User=principal;return Task.CompletedTask;}
 public Task SignOutAsync(HttpContext h,string? s,Microsoft.AspNetCore.Authentication.AuthenticationProperties? p){h.User=new ClaimsPrincipal(new ClaimsIdentity());return Task.CompletedTask;}
}
