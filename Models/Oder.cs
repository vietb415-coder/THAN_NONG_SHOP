using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;

namespace THAN_NONG_SHOP.Models
{
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
        public string Status { get; set; } = "Chờ xử lý";
        public string? UserName { get; internal set; }
    }
}
