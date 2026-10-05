using System.Net.Mail;

namespace THAN_NONG_SHOP.Services;

public static class EmailConfiguration
{
    public const string UnavailableMessage = "Dịch vụ email hiện chưa sẵn sàng. Vui lòng thử lại sau hoặc liên hệ quản trị viên.";

    public static bool IsPickup(IConfiguration config, IHostEnvironment env) =>
        env.IsDevelopment() && string.Equals(config["Email:DeliveryMethod"], "PickupDirectory", StringComparison.OrdinalIgnoreCase);

    public static string Password(IConfiguration config)
    {
        var password = config["Email:Password"] ?? "";
        // Google displays app passwords in groups separated by spaces.
        return string.Equals(config["Email:SmtpHost"]?.Trim(), "smtp.gmail.com", StringComparison.OrdinalIgnoreCase)
            ? string.Concat(password.Where(c => !char.IsWhiteSpace(c))) : password;
    }

    public static List<string> Errors(IConfiguration config, IHostEnvironment env)
    {
        var errors = new List<string>();
        if (!Uri.TryCreate(config["Site:PublicBaseUrl"]?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
            errors.Add("Site:PublicBaseUrl phải là URL website hợp lệ, không có query hoặc fragment.");
        if (!MailAddress.TryCreate(config["Email:From"], out _))
            errors.Add("Email:From chưa có địa chỉ gửi hợp lệ.");
        if (IsPickup(config, env)) return errors;
        if (!string.Equals(config["Email:DeliveryMethod"] ?? "Smtp", "Smtp", StringComparison.OrdinalIgnoreCase))
            errors.Add("Production phải dùng Email:DeliveryMethod = Smtp.");
        var host = config["Email:SmtpHost"]?.Trim();
        if (string.IsNullOrWhiteSpace(host)) errors.Add("Chưa cấu hình Email:SmtpHost.");
        var portText = config["Email:SmtpPort"] ?? "587";
        if (!int.TryParse(portText, out var port) || port < 1 || port > 65535)
            errors.Add("Email:SmtpPort không hợp lệ.");
        if (!bool.TryParse(config["Email:EnableSsl"] ?? "true", out var ssl))
            errors.Add("Email:EnableSsl phải là true hoặc false.");
        var password = Password(config);
        if (!string.IsNullOrWhiteSpace(config["Email:Username"]) &&
            (string.IsNullOrWhiteSpace(password) || password.Contains("MAT_KHAU", StringComparison.OrdinalIgnoreCase)))
            errors.Add("Email:Password đang trống hoặc là chữ mẫu. Cần điền mật khẩu ứng dụng thật.");
        if (string.Equals(host, "smtp.gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            if (!MailAddress.TryCreate(config["Email:Username"], out _))
                errors.Add("Gmail SMTP cần Email:Username là địa chỉ Gmail gửi thư.");
            if (port != 587 || !ssl) errors.Add("Gmail với SmtpClient cần cổng 587 và EnableSsl = true (STARTTLS).");
        }
        return errors;
    }
}

public sealed record EmailWorkerSnapshot(DateTime CheckedAtUtc, string Message);
public sealed class EmailWorkerStatus
{
    private EmailWorkerSnapshot? current;
    public EmailWorkerSnapshot? Current => Volatile.Read(ref current);
    public void Set(string message) => Volatile.Write(ref current, new(DateTime.UtcNow, message));
}

public sealed record EmailDiagnosticsModel(string EnvironmentName, string Host, string Sender,
    IReadOnlyList<string> Errors, int Pending, int Retrying, int Sent, EmailWorkerSnapshot? Worker);
