namespace THAN_NONG_SHOP.Models;

public sealed record PromotionResult(
    bool IsValid,
    string Code,
    string Message,
    decimal DiscountAmount,
    decimal ShippingFee,
    decimal FinalTotal);

public static class PromotionCatalog
{
    public const decimal StandardShippingFee = 30_000m;

    public static PromotionResult Calculate(string? rawCode, decimal subtotal)
    {
        var code = (rawCode ?? string.Empty).Trim().ToUpperInvariant();
        var shipping = subtotal > 0 ? StandardShippingFee : 0m;
        var totalBeforeDiscount = subtotal + shipping;

        if (string.IsNullOrEmpty(code))
            return new(false, code, "Vui lòng nhập mã khuyến mãi.", 0, shipping, totalBeforeDiscount);

        decimal minimum;
        decimal discount;
        string successMessage;
        switch (code)
        {
            case "THANNONG15":
                minimum = 299_000m;
                discount = Math.Min(decimal.Round(subtotal * 0.15m, 0), 150_000m);
                successMessage = "Giảm 15% giá trị sản phẩm (tối đa 150.000đ).";
                break;
            case "MUAVANG50":
                minimum = 499_000m;
                discount = 50_000m;
                successMessage = "Giảm trực tiếp 50.000đ.";
                break;
            case "FREESHIP":
                minimum = 199_000m;
                discount = shipping;
                successMessage = "Miễn phí vận chuyển 30.000đ.";
                break;
            case "LUCKY10":
                minimum = 199_000m;
                discount = Math.Min(decimal.Round(subtotal * 0.10m, 0), 100_000m);
                successMessage = "Giảm 10% giá trị sản phẩm (tối đa 100.000đ).";
                break;
            case "LUCKY30K":
                minimum = 299_000m;
                discount = 30_000m;
                successMessage = "Giảm trực tiếp 30.000đ.";
                break;
            case "MATONG0D":
                minimum = 499_000m;
                discount = 0m;
                successMessage = "Tặng kèm 1 chai mật ong nguyên chất 0đ trong đơn hàng.";
                break;
            case "QUA500K":
                minimum = 0m;
                discount = 0m;
                successMessage = "Giỏ quà trị giá 500.000đ sẽ được gắn với đơn hàng này.";
                break;
            case "DACBIET1TR":
                minimum = 0m;
                discount = 0m;
                successMessage = "Giỏ quà đặc biệt trị giá 1.000.000đ sẽ được gắn với đơn hàng này.";
                break;
            default:
                return new(false, code, "Mã không tồn tại hoặc không dùng để giảm tiền tại bước thanh toán.", 0, shipping, totalBeforeDiscount);
        }

        var vietnamToday = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
        if (vietnamToday > new DateTime(2026, 9, 30))
        {
            return new(false, code, "Mã khuyến mãi đã hết hạn sử dụng.", 0, shipping, totalBeforeDiscount);
        }

        if (subtotal < minimum)
        {
            return new(false, code, $"Đơn hàng cần tối thiểu {minimum:N0}đ để sử dụng mã này.", 0, shipping, totalBeforeDiscount);
        }

        discount = Math.Clamp(discount, 0, totalBeforeDiscount);
        return new(true, code, successMessage, discount, shipping, totalBeforeDiscount - discount);
    }

    public static PromotionResult Calculate(PromotionReward reward, decimal subtotal)
    {
        var shipping = subtotal > 0 ? StandardShippingFee : 0m;
        var beforeDiscount = subtotal + shipping;
        if (!reward.IsActive) return new(false, reward.TemplateCode, "Chương trình khuyến mãi đã tạm dừng.", 0, shipping, beforeDiscount);
        if (subtotal < reward.MinimumSubtotal)
            return new(false, reward.TemplateCode, $"Đơn hàng cần tối thiểu {reward.MinimumSubtotal:N0}đ để sử dụng mã này.", 0, shipping, beforeDiscount);

        var discount = reward.IsFreeShipping
            ? shipping
            : reward.FixedDiscount + decimal.Round(subtotal * reward.PercentageDiscount, 0);
        if (reward.MaximumDiscount > 0) discount = Math.Min(discount, reward.MaximumDiscount);
        discount = Math.Clamp(discount, 0, beforeDiscount);
        return new(true, reward.TemplateCode, reward.BenefitMessage, discount, shipping, beforeDiscount - discount);
    }
}
