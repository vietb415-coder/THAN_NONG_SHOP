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
    }
}
