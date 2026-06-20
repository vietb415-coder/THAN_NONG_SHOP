using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace THAN_NONG_SHOP.Migrations
{
    /// <inheritdoc />
    public partial class FixOrderPriceToDecimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Price",
                table: "OderDetails",
                newName: "price");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPrice",
                table: "Oders",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "price",
                table: "OderDetails",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "price",
                table: "OderDetails",
                newName: "Price");

            migrationBuilder.AlterColumn<double>(
                name: "TotalPrice",
                table: "Oders",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "Price",
                table: "OderDetails",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }
    }
}
