using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;

namespace THAN_NONG_SHOP.Models
{
    public static class OrderStatus
    {
        public const string Pending = "Chờ xử lý";
        public const string AwaitingPayment = "Chờ thanh toán";
        public const string Paid = "Đã thanh toán";
        public const string Shipping = "Đang giao";
        public const string Completed = "Đã hoàn thành";
        public const string Cancelled = "Đã hủy";

        public static readonly string[] All =
        [
            Pending,
            AwaitingPayment,
            Paid,
            Shipping,
            Completed,
            Cancelled
        ];
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
        public string Status { get; set; } = OrderStatus.Pending;
        public long? PayOSOrderCode { get; set; }
        public string? UserName { get; internal set; }
    }
}
