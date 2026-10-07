using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Services;

public sealed class EmailDelivery(THAN_NONG_SHOP_DbContext db, IConfiguration config, IWebHostEnvironment env,
    ILogger<EmailDelivery>? log=null, EmailWorkerStatus? status=null)
{
    public EmailMessage Queue(string recipient,string subject,string body) {
        var message=new EmailMessage { Recipient=recipient,Subject=subject,Body=body };
        db.EmailMessages.Add(message);return message;
    }
    public bool CanConfirm => EmailConfiguration.ConfirmationErrors(config).Count == 0;
    public EmailMessage Confirmation(user account)
    {
        if (!CanConfirm) throw new InvalidOperationException(EmailConfiguration.UnavailableMessage);
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        account.ConfirmationTokenHash=Hash(token);
        account.ConfirmationExpiresAt=DateTime.UtcNow.AddHours(24);
        var baseUrl=config["Site:PublicBaseUrl"]?.Trim().TrimEnd('/');
        if(!Uri.TryCreate(baseUrl,UriKind.Absolute,out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Cần cấu hình Site:PublicBaseUrl để tạo liên kết xác nhận email.");
        var message=Queue(account.Email,"Xác nhận email Thần Nông Shop",$"Xin chào {account.Fullname},\nMở liên kết sau trong vòng 24 giờ để xác nhận tài khoản:\n{baseUrl}/Account/ConfirmEmail?username={Uri.EscapeDataString(account.UserName)}&token={token}\nNếu bạn không đăng ký, hãy bỏ qua email này.");
        return message;
    }
    public async Task<bool> SendConfirmationNowAsync(EmailMessage item,CancellationToken ct=default)
    {
        try {
            await EmailTransport.SendAsync(item,config,env,ct);
            item.SentAt=DateTime.UtcNow;item.NextAttemptAt=null;
            status?.Set(EmailConfiguration.IsPickup(config,env)
                ? "Đã ghi thư thử vào thư mục local; chưa gửi ra Internet."
                : "SMTP đã tiếp nhận thư xác nhận. Vui lòng kiểm tra hộp thư và thư rác.");
        } catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch(Exception ex) {
            item.Attempts++;item.NextAttemptAt=DateTime.UtcNow.AddSeconds(15);
            status?.Set(ex is OperationCanceledException
                ? "SMTP quá 15 giây chưa phản hồi. Kiểm tra kết nối SMTP từ hosting."
                : "SMTP chưa gửi được thư xác nhận. Kiểm tra cấu hình email và log ứng dụng; hệ thống sẽ thử lại.");
            log?.LogWarning(ex,"Email xác nhận #{EmailId} chưa gửi được, sẽ thử lại.",item.Id);
        }
        await db.SaveChangesAsync(ct);
        return item.SentAt.HasValue;
    }
    public static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal static class EmailTransport
{
    public static async Task SendAsync(EmailMessage item,IConfiguration config,IWebHostEnvironment env,CancellationToken ct)
    {
        if(EmailConfiguration.Errors(config,env).Count>0)
            throw new InvalidOperationException(EmailConfiguration.UnavailableMessage);
        using var message=new MailMessage(config["Email:From"]!,item.Recipient,item.Subject,item.Body) {BodyEncoding=Encoding.UTF8,SubjectEncoding=Encoding.UTF8};
        using var client=new SmtpClient();
        if(EmailConfiguration.IsPickup(config,env)) {
            var dir=Path.GetFullPath(config["Email:PickupDirectory"] ?? Path.Combine(env.ContentRootPath,"App_Data","mail"));
            Directory.CreateDirectory(dir);client.DeliveryMethod=SmtpDeliveryMethod.SpecifiedPickupDirectory;client.PickupDirectoryLocation=dir;
        } else {
            client.Host=config["Email:SmtpHost"]!.Trim();client.Port=config.GetValue("Email:SmtpPort",587);client.EnableSsl=config.GetValue("Email:EnableSsl",true);
            client.UseDefaultCredentials=false;
            if(!string.IsNullOrEmpty(config["Email:Username"]))client.Credentials=new NetworkCredential(config["Email:Username"]!.Trim(),EmailConfiguration.Password(config));
        }
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await client.SendMailAsync(message,timeout.Token);
    }
}

public sealed class EmailOutboxWorker(IServiceScopeFactory scopes,IConfiguration config,IWebHostEnvironment env,ILogger<EmailOutboxWorker> log, EmailWorkerStatus status):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(15));
        do {
            try { await DeliverAsync(stoppingToken); }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){status.Set($"Không thể đọc/xử lý hàng đợi ({ex.GetType().Name}). Kiểm tra database và log ứng dụng.");log.LogError(ex,"Không thể xử lý hàng đợi email.");}
        } while(await timer.WaitForNextTickAsync(stoppingToken));
    }
    public async Task DeliverAsync(CancellationToken ct)
    {
        var pickup = EmailConfiguration.IsPickup(config, env);
        var errors = EmailConfiguration.Errors(config, env);
        if (errors.Count > 0)
        {
            var reason = string.Join(" ", errors);
            if (status.Current?.Message != reason) log.LogError("Email configuration: {Reason}", reason);
            status.Set(reason);
            return;
        }
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
                    client.Host=config["Email:SmtpHost"]!.Trim();client.Port=config.GetValue("Email:SmtpPort",587);client.EnableSsl=config.GetValue("Email:EnableSsl",true);
                    client.UseDefaultCredentials=false;
                    if(!string.IsNullOrEmpty(config["Email:Username"])) client.Credentials=new NetworkCredential(config["Email:Username"]?.Trim(),EmailConfiguration.Password(config));
                }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                await client.SendMailAsync(message,timeout.Token);item.SentAt=DateTime.UtcNow;
                status.Set(pickup ? "Đã ghi thư thử vào thư mục local; chưa gửi ra Internet." : "SMTP đã tiếp nhận thư gần nhất. Việc thư vào Inbox còn phụ thuộc máy chủ nhận.");
            } catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(Exception ex){
                var reason = ex switch
                {
                    OperationCanceledException => "SMTP quá 30 giây chưa phản hồi. Kiểm tra kết nối SMTP từ hosting.",
                    SmtpException smtp => $"SMTP gửi thất bại (mã {smtp.StatusCode}). Kiểm tra mật khẩu ứng dụng, tài khoản và kết nối SMTP trên hosting; xem log để biết chi tiết.",
                    _ => $"Gửi email thất bại ({ex.GetType().Name}). Xem log ứng dụng."
                };
                status.Set(reason);
                item.Attempts++;item.NextAttemptAt=DateTime.UtcNow.AddMinutes(Math.Min(60,Math.Pow(2,Math.Min(item.Attempts,6))));log.LogWarning(ex,"Email #{EmailId} chưa gửi được, sẽ thử lại.",item.Id);}
            await db.SaveChangesAsync(ct);
        }
    }
}
