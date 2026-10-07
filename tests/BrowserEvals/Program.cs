using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Services;

var failures=0;
void Check(bool ok,string name){Console.WriteLine($"{(ok?"PASS":"FAIL")} {name}");if(!ok)failures++;}
using var factory=new BrowserFactory();
factory.UseKestrel(0);
using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
using(var scope=factory.Services.CreateScope()){
 var db=scope.ServiceProvider.GetRequiredService<THAN_NONG_SHOP_DbContext>();
 db.Categories.Add(new Category{Id=1,Name="Rau củ",Description="Rau sạch"});
 var product=new Product{Id=1,Name="Cà chua test",Description="Cà chua hữu cơ",categoryId=1,price=100000,stockQuantity=5};
 product.Batches.Add(new ProductBatch{Code="BROWSER-1",RemainingQuantity=5,HarvestDate=ShopRules.Today,ExpiryDate=ShopRules.Today.AddDays(5)});
 db.Products.Add(product);
 var account=new user{UserName="browser_customer",Fullname="Khách kiểm thử",Email="browser@example.com",Phone="0912345678",RoleId=2,EmailConfirmed=true};
 account.Password=new Microsoft.AspNetCore.Identity.PasswordHasher<user>().HashPassword(account,"Test!12345");db.Users.Add(account);
 await db.SaveChangesAsync();
 db.Entry(account).Property(nameof(user.NormalizedEmail)).CurrentValue="BROWSER@EXAMPLE.COM";await db.SaveChangesAsync();
}
using var playwright=await Playwright.CreateAsync();
await using var browser=await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions{Channel=Environment.GetEnvironmentVariable("TEST_BROWSER_CHANNEL") ?? "msedge",ExecutablePath=Environment.GetEnvironmentVariable("TEST_BROWSER_PATH"),Headless=true,Args=["--no-sandbox"]});
var page=await browser.NewPageAsync(new BrowserNewPageOptions{ViewportSize=new ViewportSize{Width=390,Height=844}});
var server=factory.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
var baseUrl=server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.Single().TrimEnd('/');
await page.RouteAsync("**/*", async route=>{
 if(route.Request.Url.StartsWith(baseUrl,StringComparison.Ordinal))await route.ContinueAsync();else await route.AbortAsync();
});
var jsErrors=new List<string>();page.PageError+=(_,e)=>jsErrors.Add(e);
await page.GotoAsync(baseUrl+"/Products");
await page.EvaluateAsync("window.testPageMarker='same-document'");
await page.Locator("input[name=searchString]").FillAsync("không-tồn-tại");
await page.WaitForFunctionAsync("document.querySelector('#product-results').textContent.includes('Không tìm thấy')");
Check(await page.EvaluateAsync<bool>("window.testPageMarker==='same-document'"),"TC_24 changing search updates results without page reload");
await page.Locator("#product-filters a").ClickAsync();
await page.WaitForFunctionAsync("document.querySelector('#product-results').textContent.includes('Cà chua test')");
Check(await page.Locator("input[name=searchString]").InputValueAsync()=="","TC_23/84 reset restores catalogue and clears filters");
await page.Locator("input[name=minPrice]").FillAsync("110000");
await page.Locator("input[name=maxPrice]").FillAsync("90000");
await page.Locator("#product-filters button[type=submit]").ClickAsync();
await page.WaitForFunctionAsync("!document.querySelector('#product-results').hasAttribute('aria-busy')");
Check(await page.Locator("input[name=minPrice]").InputValueAsync()=="90000" && await page.Locator("input[name=maxPrice]").InputValueAsync()=="110000","TC_85 reversed price values corrected in visible inputs");
await page.GotoAsync(baseUrl+"/Products/Details/1");
await page.Locator("form[action='/Cart/AddToCart'] button[type=submit]").First.ClickAsync();
await page.WaitForURLAsync("**/Cart");
await page.ReloadAsync();
Check((await page.Locator("body").InnerTextAsync()).Contains("Cà chua test"),"TC_25/29 guest cart survives browser refresh");
await page.GotoAsync(baseUrl+"/Cart/Checkout");
Check(await page.Locator("input[name=paymentMethod][value=momo]").CountAsync()==0 && await page.Locator("input[name=paymentMethod][value=payos]").CountAsync()==1 && await page.Locator("input[name=paymentMethod][value=vnpay]").CountAsync()==1,"Checkout excludes removed wallet and offers payOS and VNPay");
foreach(var size in new[]{(390,844),(1280,800)}){
 await page.SetViewportSizeAsync(size.Item1,size.Item2);
 await page.EvaluateAsync("document.querySelector('form[action=\"/Cart/Checkout\"] button[type=submit]').scrollIntoView({block:'end'})");
 var overlap=await page.EvaluateAsync<bool>("""
 () => {
   const launcher=document.querySelector('#chatLauncher').getBoundingClientRect();
   return [...document.querySelectorAll('form button,form input:not([type=hidden]),form textarea,form a')].some(el=>{
     const r=el.getBoundingClientRect();
     const viewport=document.querySelector('.site-viewport')?.getBoundingClientRect();
     const visibleBottom=Math.min(r.bottom,viewport?.bottom??innerHeight);
     const visibleTop=Math.max(r.top,viewport?.top??0);
     return r.width>0 && r.height>0 && visibleBottom>visibleTop && r.left<launcher.right && r.right>launcher.left && visibleTop<launcher.bottom && visibleBottom>launcher.top;
   });
 }
 """);
 Check(!overlap,$"TC_116 launcher does not cover checkout controls at {size.Item1}px");
 await page.Locator("#chatLauncher").ClickAsync();
 Check(await page.Locator("#chatPanel").IsVisibleAsync(),$"Chat opens at {size.Item1}px");
 await page.Locator("#chatClose").ClickAsync();
 Check(!await page.Locator("#chatPanel").IsVisibleAsync(),$"Chat closes at {size.Item1}px");
 await page.EvaluateAsync("document.querySelector('form[action=\"/Cart/Checkout\"] button[type=submit]').scrollIntoView({block:'end'})");
 await page.ScreenshotAsync(new PageScreenshotOptions{Path=$"docs/test-results/checkout-{size.Item1}.png"});
}
await page.SetViewportSizeAsync(390,844);
await page.GotoAsync(baseUrl+"/Account/Login");
await page.Locator("input[name=username]").FillAsync("0912345678");
await page.Locator("input[name=password]").FillAsync("Test!12345");
await page.Locator("form[action='/Account/Login'] button[type=submit]").ClickAsync();
await page.WaitForURLAsync(baseUrl+"/");
Check((await page.Locator(".header-top").InnerTextAsync()).Contains("Khách kiểm thử"),"TC_05/07/107 phone login shows full name in header");
await page.GotoAsync(baseUrl+"/Cart");
Check((await page.Locator("body").InnerTextAsync()).Contains("Cà chua test") && await page.Locator("form[action='/Cart/UpdateQuantity']").CountAsync()==2,"TC_30/88 guest cart merges into customer cart with quantity controls");
await page.GotoAsync(baseUrl+"/Cart/Checkout");
Check(await page.Locator("input[name=customerName]").InputValueAsync()=="Khách kiểm thử","TC_91 checkout prefills customer information");
await page.Locator("input[name=customerName]").FillAsync("Khách kiểm thử");
await page.Locator("input[name=shippingPhone]").FillAsync("0912345678");
await page.Locator("textarea[name=shippingAddress]").FillAsync("123 Đường kiểm thử, Hà Nội");
await page.Locator("input[name=paymentMethod][value=cod]").CheckAsync();
await page.Locator("form[action='/Cart/Checkout'] button[type=submit]").ClickAsync();
await page.WaitForURLAsync("**/Cart/OrderSuccess");
Check((await page.Locator("body").InnerTextAsync()).Contains("Chờ xác nhận"),"TC_93 COD checkout shows order confirmation");
await page.GotoAsync(baseUrl+"/Cart");
Check(await page.Locator("form[action='/Cart/UpdateQuantity']").CountAsync()==0,"TC_93 successful checkout clears customer cart");
await page.GotoAsync(baseUrl+"/Account/Register");
Check(!page.Url.Contains("/Account/Register"),"TC_04 signed-in customer cannot open registration");
await page.GotoAsync(baseUrl+"/Admin/Product");
Check(page.Url.Contains("/Account/AccessDenied") && (await page.Locator("body").InnerTextAsync()).Contains("Không có quyền truy cập"),"TC_08 customer receives access denied instead of another login page");
await page.GotoAsync(baseUrl+"/");
await page.Locator("form[action='/Account/Logout'] button").ClickAsync();
await page.WaitForURLAsync(baseUrl+"/");
Check(!(await page.Locator(".header-top").InnerTextAsync()).Contains("Khách kiểm thử"),"TC_109 logout clears displayed identity");
Check(jsErrors.Count==0,"No browser JavaScript errors during tested flows");
Console.WriteLine($"Failures: {failures}. Browser uses isolated test data and blocks external requests.");
return failures==0?0:1;

sealed class MemoryKeys:Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository {
 private readonly List<System.Xml.Linq.XElement> keys=[];
 public IReadOnlyCollection<System.Xml.Linq.XElement> GetAllElements(){lock(keys)return keys.Select(k=>new System.Xml.Linq.XElement(k)).ToArray();}
 public void StoreElement(System.Xml.Linq.XElement element,string friendlyName){lock(keys)keys.Add(new System.Xml.Linq.XElement(element));}
}
sealed class BrowserFactory:WebApplicationFactory<EmailDelivery>{
 protected override void ConfigureWebHost(IWebHostBuilder builder){
  builder.UseEnvironment("Testing");
  var root=new DirectoryInfo(Directory.GetCurrentDirectory());while(root!=null && !File.Exists(Path.Combine(root.FullName,"THAN_NONG_SHOP.csproj")))root=root.Parent;
  builder.UseContentRoot(root?.FullName??throw new InvalidOperationException("Run from source folder."));
  builder.ConfigureLogging(log=>log.ClearProviders());
  builder.ConfigureServices(services=>{
   services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
   services.Configure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(o=>{o.XmlRepository=new MemoryKeys();o.XmlEncryptor=null;});
   foreach(var d in services.Where(d=>d.ServiceType==typeof(THAN_NONG_SHOP_DbContext) || d.ServiceType==typeof(DbContextOptions<THAN_NONG_SHOP_DbContext>) || d.ServiceType==typeof(Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<THAN_NONG_SHOP_DbContext>) || d.ImplementationType==typeof(CommerceMaintenance) || d.ImplementationType==typeof(EmailOutboxWorker)).ToList())services.Remove(d);
   services.AddDbContext<THAN_NONG_SHOP_DbContext>(o=>o.UseInMemoryDatabase("browser-test").ConfigureWarnings(w=>w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
  });
 }
}
