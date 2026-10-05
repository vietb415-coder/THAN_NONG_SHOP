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
var token=confirmation.Body.Split("&token=")[1].Split('\n')[0];var confirm=Account();await confirm.ConfirmEmail(created.UserName,token);
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
