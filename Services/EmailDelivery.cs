using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Services;

public sealed class EmailDelivery(THAN_NONG_SHOP_DbContext db, IConfiguration config)
{
    public void Queue(string recipient,string subject,string body) => db.EmailMessages.Add(new EmailMessage { Recipient=recipient,Subject=subject,Body=body });
    public void Confirmation(user account)
    {
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        account.ConfirmationTokenHash=Hash(token);
        account.ConfirmationExpiresAt=DateTime.UtcNow.AddHours(24);
        var baseUrl=config["Site:PublicBaseUrl"]?.TrimEnd('/');
        if(!Uri.TryCreate(baseUrl,UriKind.Absolute,out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Cần cấu hình Site:PublicBaseUrl để tạo liên kết xác nhận email.");
        Queue(account.Email,"Xác nhận email Thần Nông Shop",$"Xin chào {account.Fullname},\nMở liên kết sau trong vòng 24 giờ để xác nhận tài khoản:\n{baseUrl}/Account/ConfirmEmail?username={Uri.EscapeDataString(account.UserName)}&token={token}\nNếu bạn không đăng ký, hãy bỏ qua email này.");
    }
    public static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class EmailOutboxWorker(IServiceScopeFactory scopes,IConfiguration config,IWebHostEnvironment env,ILogger<EmailOutboxWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(15));
        do {
            try { await DeliverAsync(stoppingToken); }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){log.LogError(ex,"Không thể xử lý hàng đợi email.");}
        } while(await timer.WaitForNextTickAsync(stoppingToken));
    }
    public async Task DeliverAsync(CancellationToken ct)
    {
        var pickup=env.IsDevelopment() && config["Email:DeliveryMethod"]=="PickupDirectory";
        if(!pickup && string.IsNullOrWhiteSpace(config["Email:SmtpHost"])) return;
        using var scope=scopes.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<THAN_NONG_SHOP_DbContext>();
        var now=DateTime.UtcNow;
        var messages=await db.EmailMessages.Where(m=>m.SentAt==null && (m.NextAttemptAt==null || m.NextAttemptAt<=now)).OrderBy(m=>m.Id).Take(20).ToListAsync(ct);
        foreach(var item in messages) {
            try {
                ct.ThrowIfCancellationRequested();
                using var message=new MailMessage(config["Email:From"] ?? "noreply@thannong.example",item.Recipient,item.Subject,item.Body) {BodyEncoding=Encoding.UTF8,SubjectEncoding=Encoding.UTF8};
                using var client=new SmtpClient();
                if(pickup) {
                    var dir=Path.GetFullPath(config["Email:PickupDirectory"] ?? Path.Combine(env.ContentRootPath,"App_Data","mail"));
                    Directory.CreateDirectory(dir);client.DeliveryMethod=SmtpDeliveryMethod.SpecifiedPickupDirectory;client.PickupDirectoryLocation=dir;
                } else {
                    client.Host=config["Email:SmtpHost"]!;client.Port=config.GetValue("Email:SmtpPort",587);client.EnableSsl=true;
                    client.UseDefaultCredentials=false;
                    if(!string.IsNullOrEmpty(config["Email:Username"])) client.Credentials=new NetworkCredential(config["Email:Username"],config["Email:Password"]);
                }
                await client.SendMailAsync(message,ct);item.SentAt=DateTime.UtcNow;
            } catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(Exception ex){item.Attempts++;item.NextAttemptAt=DateTime.UtcNow.AddMinutes(Math.Min(60,Math.Pow(2,Math.Min(item.Attempts,6))));log.LogWarning(ex,"Email #{EmailId} chưa gửi được, sẽ thử lại.",item.Id);}
            await db.SaveChangesAsync(ct);
        }
    }
}
