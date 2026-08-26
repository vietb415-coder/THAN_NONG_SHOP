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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Oder>()
                .HasIndex(order => order.PayOSOrderCode)
                .IsUnique()
                .HasFilter("[PayOSOrderCode] IS NOT NULL");
            modelBuilder.Entity<ChatConversation>().HasMany(c => c.Messages).WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ChatConversation>().HasIndex(c => c.LastMessageAt);
            modelBuilder.Entity<ChatStoredMessage>().HasIndex(m => new { m.ConversationId, m.CreatedAt });
        }
    }
}
