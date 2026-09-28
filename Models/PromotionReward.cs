using System.ComponentModel.DataAnnotations;

namespace THAN_NONG_SHOP.Models;

public sealed class PromotionReward
{
    public int Id { get; set; }
    [MaxLength(30)] public required string TemplateCode { get; set; }
    [MaxLength(100)] public required string Title { get; set; }
    [MaxLength(300)] public required string Description { get; set; }
    [MaxLength(300)] public required string BenefitMessage { get; set; }
    public decimal MinimumSubtotal { get; set; }
    public decimal PercentageDiscount { get; set; }
    public decimal FixedDiscount { get; set; }
    public decimal MaximumDiscount { get; set; }
    public bool IsFreeShipping { get; set; }
    public bool IsGift { get; set; }
    [MaxLength(120)] public string? GiftName { get; set; }
    public bool IsPublicOffer { get; set; }
    public bool IsActive { get; set; } = true;
    public int WheelWeight { get; set; }
    public int? StockRemaining { get; set; }
}
