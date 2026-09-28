using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace THAN_NONG_SHOP.Data;
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<THAN_NONG_SHOP_DbContext>
{
    public THAN_NONG_SHOP_DbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<THAN_NONG_SHOP_DbContext>().UseSqlServer("Server=.\\SQLEXPRESS;Database=THANNONG_MigrationOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
}
