using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Services;
using System.Net;
using System.Net.Sockets;
using System.Text;

var failures = 0;
void Check(bool ok, string name) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}"); if(!ok) failures++; }
var env = new TestEnvironment();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["Email:DeliveryMethod"]="Smtp", ["Email:SmtpHost"]="smtp.gmail.com", ["Email:SmtpPort"]="587",
    ["Email:EnableSsl"]="true", ["Email:From"]="sender@example.com", ["Email:Username"]="sender@example.com",
    ["Email:Password"]="DIEN_MAT_KHAU_UNG_DUNG_MOI", ["Site:PublicBaseUrl"]="https://shop.example.com"
}).Build();
Check(EmailConfiguration.Errors(config,env).Any(x=>x.Contains("Password")),"Reject placeholder password");
config["Email:Password"]="abcd efgh ijkl mnop";
Check(EmailConfiguration.Password(config)=="abcdefghijklmnop","Normalize grouped Gmail app password");
Check(EmailConfiguration.Errors(config,env).Count==0,"Accept complete Gmail configuration without claiming authentication");
config["Email:SmtpPort"]="465";
Check(EmailConfiguration.Errors(config,env).Count>0,"Reject implicit SSL port for Gmail SmtpClient");
config["Email:SmtpPort"]="not-a-port";
Check(EmailConfiguration.Errors(config,env).Count>0,"Invalid port is reported without throwing");
config["Email:SmtpPort"]="587";
config["Site:PublicBaseUrl"]="https://shop.example.com?bad=1";
Check(EmailConfiguration.Errors(config,env).Count>0,"Reject malformed confirmation base URL");
config["Site:PublicBaseUrl"]="https://shop.example.com";
config["Email:DeliveryMethod"]="PickupDirectory";
Check(EmailConfiguration.Errors(config,env).Count>0,"Reject pickup mode in Production");
env.EnvironmentName="Development";
var pickup=Path.Combine(Path.GetTempPath(),"thannong-email-test-"+Guid.NewGuid());
config["Email:PickupDirectory"]=pickup;
var services = new ServiceCollection();
services.AddDbContext<THAN_NONG_SHOP_DbContext>(o=>o.UseInMemoryDatabase("email-"+Guid.NewGuid()),ServiceLifetime.Singleton);
using var provider=services.BuildServiceProvider();
var db=provider.GetRequiredService<THAN_NONG_SHOP_DbContext>();
var mail=new EmailDelivery(db,config,env);
var account=new user {UserName="test_user",Fullname="Test User",Email="recipient@example.com"};
mail.Confirmation(account);
await db.SaveChangesAsync();
var queued=await db.EmailMessages.SingleAsync();
var token=queued.Body.Split("&token=")[1].Split('\n')[0];
Check(token.Length==64 && account.ConfirmationTokenHash==EmailDelivery.Hash(token),"Confirmation token matches hash and queued link");
Check(account.ConfirmationExpiresAt>DateTime.UtcNow.AddHours(23),"Confirmation token expires after approximately 24 hours");
var status=new EmailWorkerStatus();
var worker=new EmailOutboxWorker(provider.GetRequiredService<IServiceScopeFactory>(),config,env,NullLogger<EmailOutboxWorker>.Instance,status);
await worker.DeliverAsync(CancellationToken.None);
Check(queued.SentAt!=null && Directory.GetFiles(pickup).Length==1,"Worker delivers queued email to local pickup file");
await worker.DeliverAsync(CancellationToken.None);
Check(Directory.GetFiles(pickup).Length==1,"Worker does not resend delivered messages");
Directory.Delete(pickup,true);

// Real SMTP dialogue against a loopback test server; no Internet mail or real credentials.
env.EnvironmentName="Production";
config["Email:DeliveryMethod"]="Smtp";
config["Email:SmtpHost"]="127.0.0.1";
config["Email:EnableSsl"]="false";
config["Email:Username"]="";
config["Email:Password"]="";
using var listener=new TcpListener(IPAddress.Loopback,0);
listener.Start();
config["Email:SmtpPort"]=((IPEndPoint)listener.LocalEndpoint).Port.ToString();
using var smtpDeadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
var smtpTask=Task.Run(async()=>{
 using var socket=await listener.AcceptTcpClientAsync(smtpDeadline.Token);
 using var stream=socket.GetStream();
 using var reader=new StreamReader(stream,Encoding.ASCII);
 using var writer=new StreamWriter(stream,Encoding.ASCII){NewLine="\r\n",AutoFlush=true};
 await writer.WriteLineAsync("220 localhost test SMTP");
 while(await reader.ReadLineAsync(smtpDeadline.Token) is { } line){
  if(line.StartsWith("DATA")){
   await writer.WriteLineAsync("354 End with dot");
   while(await reader.ReadLineAsync(smtpDeadline.Token) is { } body && body!="."){}
   await writer.WriteLineAsync("250 queued");
  }else if(line.StartsWith("QUIT")){await writer.WriteLineAsync("221 bye");break;}
  else await writer.WriteLineAsync("250 OK");
 }
});
mail.Queue("recipient@example.com","Loopback SMTP test","Test only");
await db.SaveChangesAsync();
await worker.DeliverAsync(smtpDeadline.Token);
await smtpTask;
Check(await db.EmailMessages.CountAsync(m=>m.SentAt!=null)==2,"SMTP dialogue succeeds with configured EnableSsl=false on local server");
listener.Stop();
mail.Queue("recipient@example.com","Retry test","Test only");
await db.SaveChangesAsync();
await worker.DeliverAsync(CancellationToken.None);
var failed=await db.EmailMessages.SingleAsync(m=>m.SentAt==null);
Check(failed.Attempts==1 && failed.NextAttemptAt>DateTime.UtcNow,"Connection failure schedules retry without marking sent");
Check(status.Current?.Message.Contains("SMTP gửi thất bại")==true,"Admin status reports delivery failure");
await worker.DeliverAsync(CancellationToken.None);
Check(failed.Attempts==1,"Retry backoff is respected");
config["Email:SmtpHost"]="smtp.gmail.com";
config["Email:SmtpPort"]="587";
config["Email:EnableSsl"]="true";
config["Email:Username"]="sender@example.com";
config["Email:Password"]="DIEN_MAT_KHAU_UNG_DUNG_MOI";
var count=await db.EmailMessages.CountAsync();
try{mail.Confirmation(account);Check(false,"Block confirmation with invalid configuration");}
catch(InvalidOperationException){Check(await db.EmailMessages.CountAsync()==count,"Block confirmation without queueing a misleading email");}
var protector = new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();
var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
var controller = new THAN_NONG_SHOP.Controllers.AccountController(db,
    new Microsoft.AspNetCore.Identity.PasswordHasher<user>(), protector, mail,
    new CartState(db, new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = http }, protector));
controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http };
controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http, new MemoryTempData());
var beforeUsers = await db.Users.CountAsync();
var registration = await controller.Register("test_register", "Test Register", "new@example.com", "0912345678", "Example!123", "Example!123");
Check(registration is Microsoft.AspNetCore.Mvc.ViewResult && !controller.ModelState.IsValid
    && await db.Users.CountAsync() == beforeUsers, "Registration does not create a locked account when SMTP configuration is missing");
await controller.ResendConfirmation("new@example.com");
Check(controller.TempData["AccountMessage"]?.ToString() == EmailConfiguration.UnavailableMessage,
    "Resend reports unavailable configuration rather than claiming delivery");
if (PromotionCatalog.HasCampaignEnded())
{
    var home = new THAN_NONG_SHOP.Controllers.HomeController(db, protector);
    Check(await home.ClaimPromotion("THANNONG15", CancellationToken.None) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,
        "Expired campaign cannot issue a voucher");
    Check(await home.SpinPromotion(CancellationToken.None) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,
        "Expired campaign cannot consume a spin or reward stock");
}
using (var factory = new TestWebFactory())
using (var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
{
    foreach (var route in new[] { "/Account/Login", "/Account/Register", "/Home/Promotions" })
    {
        var response = await client.GetAsync(route);
        Check(response.StatusCode == HttpStatusCode.OK, $"HTTP render {route} with isolated in-memory database");
    }
    var unauthorized = await client.GetAsync("/Admin/Email");
    Check(unauthorized.StatusCode == HttpStatusCode.Redirect, "Anonymous visitor cannot read admin email diagnostics");
}
Console.WriteLine($"Failures: {failures}");
return failures==0?0:1;

sealed class TestEnvironment : IWebHostEnvironment {
 public string EnvironmentName {get;set;}="Production";
 public string ApplicationName {get;set;}="EmailEvals";
 public string ContentRootPath {get;set;}=Path.GetTempPath();
 public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
 public string WebRootPath {get;set;}=Path.GetTempPath();
 public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider();
}

sealed class MemoryTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
{
    public IDictionary<string,object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string,object>();
    public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string,object> values) { }
}

sealed class MemoryKeys:Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository
{
    private readonly List<System.Xml.Linq.XElement> keys=[];
    public IReadOnlyCollection<System.Xml.Linq.XElement> GetAllElements(){lock(keys)return keys.Select(k=>new System.Xml.Linq.XElement(k)).ToArray();}
    public void StoreElement(System.Xml.Linq.XElement element,string friendlyName){lock(keys)keys.Add(new System.Xml.Linq.XElement(element));}
}

sealed class TestWebFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<EmailDelivery>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Find project from either root or test working directory.
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (root != null && !File.Exists(Path.Combine(root.FullName, "THAN_NONG_SHOP.csproj"))) root = root.Parent;
        builder.UseContentRoot(root?.FullName ?? throw new InvalidOperationException("Run from the source folder."));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
            services.Configure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(o=>{o.XmlRepository=new MemoryKeys();o.XmlEncryptor=null;});
            foreach(var descriptor in services.Where(d => d.ServiceType == typeof(THAN_NONG_SHOP_DbContext)
                || d.ServiceType == typeof(DbContextOptions<THAN_NONG_SHOP_DbContext>)
                || d.ServiceType == typeof(Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<THAN_NONG_SHOP_DbContext>)
                || d.ImplementationType == typeof(CommerceMaintenance)
                || d.ImplementationType == typeof(EmailOutboxWorker)).ToList()) services.Remove(descriptor);
            services.AddDbContext<THAN_NONG_SHOP_DbContext>(options => options.UseInMemoryDatabase("http-smoke-test"));
        });
    }
}
