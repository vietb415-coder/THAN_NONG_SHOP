namespace THAN_NONG_SHOP.Models;

public sealed class AccountVoucherViewModel
{
    public required string Code { get; init; }
    public required string TemplateCode { get; init; }
    public required string Benefit { get; init; }
    public required string Status { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int? OrderId { get; init; }
}

public sealed class AccountProfileViewModel
{
    public required user User { get; init; }
    public required IReadOnlyList<AccountVoucherViewModel> Vouchers { get; init; }
    public required IReadOnlyList<Oder> RecentOrders { get; init; }
}
