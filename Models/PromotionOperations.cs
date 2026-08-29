using System.ComponentModel.DataAnnotations;

namespace THAN_NONG_SHOP.Models;

public sealed class PromotionSpin
{
    public long Id { get; set; }
    [MaxLength(100)] public required string UserName { get; set; }
    public DateOnly SpinDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RewardId { get; set; }
    public PromotionReward? Reward { get; set; }
    public int VoucherId { get; set; }
    public PromotionVoucher? Voucher { get; set; }
}

public sealed class PromotionVoucherEvent
{
    public long Id { get; set; }
    public int VoucherId { get; set; }
    public PromotionVoucher? Voucher { get; set; }
    [MaxLength(30)] public required string EventType { get; set; }
    [MaxLength(100)] public required string UserName { get; set; }
    public int? OrderId { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class OrderGiftItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Oder? Order { get; set; }
    public int PromotionVoucherId { get; set; }
    public PromotionVoucher? PromotionVoucher { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public static class VoucherEventTypes
{
    public const string Issued = "Issued";
    public const string Revoked = "Revoked";
    public const string Applied = "Applied";
    public const string Reserved = "Reserved";
    public const string Used = "Used";
    public const string Released = "Released";
}
