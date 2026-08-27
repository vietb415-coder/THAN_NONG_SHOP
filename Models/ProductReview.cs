using System.ComponentModel.DataAnnotations;

namespace THAN_NONG_SHOP.Models;

public sealed class ProductReview
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, StringLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; }

    [Required, StringLength(1000, MinimumLength = 3)]
    public string Comment { get; set; } = string.Empty;

    public bool IsVerifiedPurchase { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ProductDetailsViewModel
{
    public required Product Product { get; init; }
    public IReadOnlyList<ProductReview> Reviews { get; init; } = [];
    public double AverageRating { get; init; }
    public bool CanReview { get; init; }
    public ProductReview? CurrentUserReview { get; init; }
}
