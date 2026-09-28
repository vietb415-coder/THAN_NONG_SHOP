using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace THAN_NONG_SHOP.Models

{
    public class Product
    {
        [Key]
        public int Id { get; set; }
        [Required, StringLength(200)] public string Name { get; set; } = "";
        [Required, StringLength(5000)] public String Description { get; set; } = "";
        [Range(typeof(decimal), "1", "1000000000", ErrorMessage = "Giá bán phải lớn hơn 0 và không vượt 1 tỷ đồng.")]
        public decimal price { get; set; }
        public string? ImageUrl { get; set; }
        public string? farmerStory { get; set; }
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho không được âm.")]
        public int stockQuantity { get; set; }
        [Required, StringLength(30)] public string Unit { get; set; } = "kg";
        [StringLength(500)] public string? Certification { get; set; }
        [MaxLength(100)] public string? SellerUserName { get; set; }
        [Timestamp] public byte[] Version { get; set; } = [];
        public ICollection<ProductBatch> Batches { get; set; } = [];

        [Range(1,int.MaxValue,ErrorMessage="Vui lòng chọn danh mục.")] public int categoryId { get; set; }
        [ForeignKey ("categoryId")]
        public virtual Category? Category { get; set; }
        public ICollection<ProductReview> Reviews { get; set; } = [];

    }

    public class ProductManagementViewModel
    {
        // Gộp cả 2 danh sách bạn cần vào đây
        public IEnumerable<Product> Products { get; set; }
        public IEnumerable<Category> Categories { get; set; }
    }
}
