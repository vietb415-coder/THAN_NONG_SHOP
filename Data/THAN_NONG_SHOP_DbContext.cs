using THAN_NONG_SHOP.Models;
using Microsoft.EntityFrameworkCore;


namespace THAN_NONG_SHOP.Data
{
    public class THAN_NONG_SHOP_DbContext : DbContext
    {
        public THAN_NONG_SHOP_DbContext(DbContextOptions<THAN_NONG_SHOP_DbContext> options) : base(options)
        {
        }
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<user> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Oder> Oders { get; set; }
        public DbSet<OderDetail> OderDetails { get; set; }
        public DbSet<ChatKnowledge> ChatKnowledge { get; set; }
        public DbSet<ChatConversation> ChatConversations { get; set; }
        public DbSet<ChatStoredMessage> ChatMessages { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }
        public DbSet<PromotionVoucher> PromotionVouchers { get; set; }
        public DbSet<PromotionReward> PromotionRewards { get; set; }
        public DbSet<PromotionSpin> PromotionSpins { get; set; }
        public DbSet<PromotionVoucherEvent> PromotionVoucherEvents { get; set; }
        public DbSet<OrderGiftItem> OrderGiftItems { get; set; }
        public DbSet<ProductBatch> ProductBatches { get; set; }
        public DbSet<BatchAllocation> BatchAllocations { get; set; }
        public DbSet<SavedCartItem> SavedCartItems { get; set; }
        public DbSet<ReviewMedia> ReviewMedia { get; set; }
        public DbSet<EmailMessage> EmailMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<user>().Property(u=>u.NormalizedEmail).HasComputedColumnSql("UPPER(LTRIM(RTRIM([Email])))",stored:true);
            modelBuilder.Entity<user>().HasIndex(u=>u.NormalizedEmail).IsUnique();
            modelBuilder.Entity<user>().HasIndex(u=>u.Phone).IsUnique();
            modelBuilder.Entity<user>().HasIndex(u=>u.UserName).IsUnique();
            modelBuilder.Entity<Oder>().HasIndex(o=>o.PaymentReference).IsUnique().HasFilter("[PaymentReference] IS NOT NULL");
            modelBuilder.Entity<Oder>().HasIndex(o=>o.CheckoutToken).IsUnique().HasFilter("[CheckoutToken] IS NOT NULL");
            modelBuilder.Entity<OderDetail>().Property(d=>d.SellerUserName).HasMaxLength(100);
            modelBuilder.Entity<ProductBatch>().HasIndex(b=>b.Code).IsUnique();
            modelBuilder.Entity<ProductBatch>().HasOne(b=>b.Product).WithMany(p=>p.Batches).HasForeignKey(b=>b.ProductId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<BatchAllocation>().HasOne(a=>a.OrderDetail).WithMany(d=>d.Allocations).HasForeignKey(a=>a.OrderDetailId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<BatchAllocation>().HasOne(a=>a.ProductBatch).WithMany().HasForeignKey(a=>a.ProductBatchId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<SavedCartItem>().HasIndex(c=>new {c.UserName,c.ProductId}).IsUnique();
            modelBuilder.Entity<SavedCartItem>().HasOne(c=>c.Product).WithMany().HasForeignKey(c=>c.ProductId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ReviewMedia>().HasOne(m=>m.ProductReview).WithMany(r=>r.Media).HasForeignKey(m=>m.ProductReviewId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<EmailMessage>().HasIndex(m=>new {m.SentAt,m.NextAttemptAt});

            modelBuilder.Entity<Oder>()
                .HasIndex(order => order.PayOSOrderCode)
                .IsUnique()
                .HasFilter("[PayOSOrderCode] IS NOT NULL");
            modelBuilder.Entity<Oder>().Property(order => order.TotalPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Oder>().Property(order => order.Subtotal).HasPrecision(18, 2);
            modelBuilder.Entity<Oder>().Property(order => order.ShippingFee).HasPrecision(18, 2);
            modelBuilder.Entity<Oder>().Property(order => order.DiscountAmount).HasPrecision(18, 2);
            modelBuilder.Entity<OderDetail>().Property(detail => detail.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(product => product.price).HasPrecision(18, 2);
            modelBuilder.Entity<ProductReview>().HasIndex(review => new { review.ProductId, review.UserName }).IsUnique();
            modelBuilder.Entity<ProductReview>().HasOne(review => review.Product).WithMany(product => product.Reviews)
                .HasForeignKey(review => review.ProductId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ChatConversation>().HasMany(c => c.Messages).WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ChatConversation>().HasIndex(c => c.LastMessageAt);
            modelBuilder.Entity<ChatStoredMessage>().HasIndex(m => new { m.ConversationId, m.CreatedAt });
            modelBuilder.Entity<PromotionVoucher>().HasIndex(voucher => voucher.CodeHash).IsUnique();
            modelBuilder.Entity<PromotionVoucher>().HasIndex(voucher => new { voucher.UserName, voucher.UsedAt });
            modelBuilder.Entity<PromotionVoucher>().HasOne(voucher => voucher.Reward).WithMany().HasForeignKey(voucher => voucher.RewardId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<PromotionReward>().HasIndex(reward => reward.TemplateCode).IsUnique();
            modelBuilder.Entity<PromotionReward>().Property(reward => reward.MinimumSubtotal).HasPrecision(18, 2);
            modelBuilder.Entity<PromotionReward>().Property(reward => reward.PercentageDiscount).HasPrecision(8, 4);
            modelBuilder.Entity<PromotionReward>().Property(reward => reward.FixedDiscount).HasPrecision(18, 2);
            modelBuilder.Entity<PromotionReward>().Property(reward => reward.MaximumDiscount).HasPrecision(18, 2);
            modelBuilder.Entity<PromotionSpin>().HasIndex(spin => new { spin.UserName, spin.SpinDate }).IsUnique();
            modelBuilder.Entity<PromotionSpin>().HasOne(spin => spin.Reward).WithMany().HasForeignKey(spin => spin.RewardId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PromotionSpin>().HasOne(spin => spin.Voucher).WithMany().HasForeignKey(spin => spin.VoucherId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PromotionVoucherEvent>().HasIndex(item => new { item.VoucherId, item.CreatedAt });
            modelBuilder.Entity<PromotionVoucherEvent>().HasOne(item => item.Voucher).WithMany().HasForeignKey(item => item.VoucherId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<OrderGiftItem>().Property(item => item.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<OrderGiftItem>().HasOne(item => item.Order).WithMany().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<OrderGiftItem>().HasOne(item => item.PromotionVoucher).WithMany().HasForeignKey(item => item.PromotionVoucherId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
