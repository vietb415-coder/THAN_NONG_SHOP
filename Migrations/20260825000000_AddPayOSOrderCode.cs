using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using THAN_NONG_SHOP.Data;

#nullable disable

namespace THAN_NONG_SHOP.Migrations
{
    [DbContext(typeof(THAN_NONG_SHOP_DbContext))]
    [Migration("20260825000000_AddPayOSOrderCode")]
    public partial class AddPayOSOrderCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PayOSOrderCode",
                table: "Oders",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Oders_PayOSOrderCode",
                table: "Oders",
                column: "PayOSOrderCode",
                unique: true,
                filter: "[PayOSOrderCode] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Oders_PayOSOrderCode",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "PayOSOrderCode",
                table: "Oders");
        }
    }
}
