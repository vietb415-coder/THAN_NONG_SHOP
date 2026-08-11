using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace THAN_NONG_SHOP.Models

{
    public class Product
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }   
        public String Description { get; set; }
        public decimal price { get; set; }
        public string? ImageUrl { get; set; }
        public string? farmerStory { get; set; }
        public int stockQuantity { get; set; }

        public int categoryId { get; set; }
        [ForeignKey ("categoryId")]
        public virtual Category? Category { get; set; }

    }

    public class ProductManagementViewModel
    {
        // Gộp cả 2 danh sách bạn cần vào đây
        public IEnumerable<Product> Products { get; set; }
        public IEnumerable<Category> Categories { get; set; }
    }
}
