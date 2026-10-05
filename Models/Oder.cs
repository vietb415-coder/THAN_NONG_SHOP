using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;

namespace THAN_NONG_SHOP.Models
{
    public static class OrderStatus
    {
        public const string Pending = "Chờ xác nhận";
        public const string Packing = "Đang đóng gói";
        public const string AwaitingPayment = "Chờ thanh toán";
        public const string Paid = "Đã thanh toán";
        public const string Shipping = "Đang giao";
        public const string Completed = "Đã giao";
        public const string Cancelled = "Đã hủy";

        public static readonly string[] All =
        [
            Pending,
            Packing,
            AwaitingPayment,
            Paid,
            Shipping,
            Completed,
            Cancelled
        ];
        public static bool CanMove(string current,string next) => current == next || current switch
        {
            Pending or Paid => next is Packing or Cancelled,
            Packing => next == Shipping,
            Shipping => next == Completed,
            _ => false
        };
        public static IEnumerable<string> Next(string current) => All.Where(next => CanMove(current,next));
        public static bool CanCustomerCancel(string status,IEnumerable<string> lineStatuses) =>
            status is Pending or Packing && !lineStatuses.Any(s=>s is Shipping or Completed);
    }

    public class Oder
    {
        public int Id { get; set; }
        [Required(ErrorMessage ="Vui lòng nhập tên người nhận")]
        public string CustomerName { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string PhoneNumber { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
        public string Address { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; }
        [MaxLength(30)] public string? PromotionTemplateCode { get; set; }
        public string Status { get; set; } = OrderStatus.Pending;
        public long? PayOSOrderCode { get; set; }
        [MaxLength(20)] public string PaymentMethod { get; set; } = "cod";
        [MaxLength(64)] public string? PaymentReference { get; set; }
        [MaxLength(32)] public string? CheckoutToken { get; set; }
        [MaxLength(2000)] public string? PaymentUrl { get; set; }
        public DateTime? PaymentExpiresAt { get; set; }
        [MaxLength(20)] public string ShippingMethod { get; set; } = "standard";
        public bool InventoryRestored { get; set; }
        public bool PaymentNeedsReview { get; set; }
        public DateTime? CompletedAt { get; set; }
        [Timestamp] public byte[] Version { get; set; } = [];
        public string? UserName { get; internal set; }
    }
}
