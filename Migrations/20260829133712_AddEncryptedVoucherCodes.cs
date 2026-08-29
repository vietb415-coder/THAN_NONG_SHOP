using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace THAN_NONG_SHOP.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptedVoucherCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedCode",
                table: "PromotionVouchers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedCode",
                table: "PromotionVouchers");
        }
    }
}
