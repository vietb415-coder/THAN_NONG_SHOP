using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace THAN_NONG_SHOP.Models;

public sealed class PromotionVoucher
{
    public int Id { get; set; }
    [MaxLength(64)] public required string CodeHash { get; set; }
    [MaxLength(500)] public string? ProtectedCode { get; set; }
    [MaxLength(100)] public required string UserName { get; set; }
    [MaxLength(30)] public required string TemplateCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int? OrderId { get; set; }
    public int? RewardId { get; set; }
    public PromotionReward? Reward { get; set; }
}

public static class VoucherSecurity
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string GenerateCode()
    {
        Span<byte> bytes = stackalloc byte[12];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[14];
        chars[0] = 'T'; chars[1] = 'N';
        for (var i = 0; i < bytes.Length; i++) chars[i + 2] = Alphabet[bytes[i] % Alphabet.Length];
        return new string(chars);
    }

    public static string HashCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
