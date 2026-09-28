namespace THAN_NONG_SHOP.Models
{
    public class OderDetail
    {
        public int Id { get; set; }
        public int OderId { get; set; }
        public  Oder? Oder { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string FulfillmentStatus { get; set; } = OrderStatus.Pending;
        public string? SellerUserName { get; set; }
        public ICollection<BatchAllocation> Allocations { get; set; } = [];
    }
}
