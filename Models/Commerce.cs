using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace THAN_NONG_SHOP.Models;

public sealed class ProductBatch
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    [Required, StringLength(60)] public string Code { get; set; } = "";
    public DateTime? HarvestDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    [Range(0, int.MaxValue)] public int RemainingQuantity { get; set; }
    public bool IsLegacy { get; set; }
    public bool IsNearExpiry { get; set; }
    [Timestamp] public byte[] Version { get; set; } = [];
}
public sealed class BatchAllocation
{
    public int Id { get; set; }
    public int OrderDetailId { get; set; }
    public OderDetail OrderDetail { get; set; } = null!;
    public int ProductBatchId { get; set; }
    public ProductBatch ProductBatch { get; set; } = null!;
    public int Quantity { get; set; }
}
public sealed class SavedCartItem
{
    public int Id { get; set; }
    [MaxLength(100)] public string UserName { get; set; } = "";
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    [Range(1,999)] public int Quantity { get; set; }
}
public sealed class ReviewMedia
{
    public int Id { get; set; }
    public int ProductReviewId { get; set; }
    public ProductReview ProductReview { get; set; } = null!;
    [MaxLength(100)] public string FileName { get; set; } = "";
    [MaxLength(40)] public string ContentType { get; set; } = "";
    public bool IsVideo { get; set; }
}
public sealed class EmailMessage
{
    public long Id { get; set; }
    [MaxLength(254)] public string Recipient { get; set; } = "";
    [MaxLength(200)] public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public int Attempts { get; set; }
}
public sealed record SalesPoint(string Label, decimal Revenue);
public sealed record TopProduct(int Id,string Name,int Quantity,decimal Revenue);
public sealed class SalesReport
{
    public string Period { get; set; } = "month";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
    public List<SalesPoint> Points { get; set; } = [];
    public List<TopProduct> TopProducts { get; set; } = [];
    public List<ProductBatch> NearExpiry { get; set; } = [];
}
public static class ShopRules
{
    public static DateTime Today => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
    public static string Role(user u) => u.RoleId == 1 ? "Admin" : u.RoleId == 3 && u.SellerApproved ? "Seller" : "User";
    public static bool StrongPassword(string? value) => value is { Length: >= 8 and <= 128 }
        && value.Any(char.IsUpper) && value.Any(char.IsLower) && value.Any(char.IsDigit)
        && value.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
    public static string NormalizeEmail(string value) => value.Trim().ToUpperInvariant();
    public static bool ValidBatch(string code, DateTime? harvest, DateTime? expiry, int quantity) =>
        !string.IsNullOrWhiteSpace(code) && code.Trim().Length <= 60 && harvest.HasValue && expiry.HasValue
        && expiry.Value.Date > harvest.Value.Date && expiry.Value.Date >= Today && quantity > 0;
    public static bool NearExpiry(DateTime? expiry,DateTime today) => expiry.HasValue && expiry.Value.Date >= today.Date && expiry.Value.Date < today.Date.AddDays(3);
    public static (DateTime Start,DateTime End) Period(string period, DateTime day)
    {
        day=day.Date;
        return period switch {
            "day" => (day,day.AddDays(1)),
            "week" => (day.AddDays(-((int)day.DayOfWeek+6)%7),day.AddDays(-((int)day.DayOfWeek+6)%7+7)),
            "quarter" => (new(day.Year,((day.Month-1)/3)*3+1,1),new DateTime(day.Year,((day.Month-1)/3)*3+1,1).AddMonths(3)),
            _ => (new(day.Year,day.Month,1),new DateTime(day.Year,day.Month,1).AddMonths(1))
        };
    }
}
public static class ShippingMethods
{
    public static readonly Dictionary<string,string> Names = new() { ["standard"]="Giao thường",["express"]="Giao nhanh",["cold"]="Giao lạnh" };
    public static decimal Fee(string method) => method switch {"standard"=>30_000m,"express"=>50_000m,"cold"=>70_000m,_=>throw new ArgumentException("Phương thức giao hàng không hợp lệ.")};
}
