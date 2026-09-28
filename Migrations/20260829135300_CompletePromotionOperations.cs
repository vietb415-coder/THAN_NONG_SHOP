using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace THAN_NONG_SHOP.Migrations
{
    /// <inheritdoc />
    public partial class CompletePromotionOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RewardId",
                table: "PromotionVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Oders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PromotionTemplateCode",
                table: "Oders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFee",
                table: "Oders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Oders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "OrderGiftItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    PromotionVoucherId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderGiftItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderGiftItems_Oders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Oders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderGiftItems_PromotionVouchers_PromotionVoucherId",
                        column: x => x.PromotionVoucherId,
                        principalTable: "PromotionVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromotionRewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    BenefitMessage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MinimumSubtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PercentageDiscount = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    FixedDiscount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumDiscount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsFreeShipping = table.Column<bool>(type: "bit", nullable: false),
                    IsGift = table.Column<bool>(type: "bit", nullable: false),
                    GiftName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsPublicOffer = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    WheelWeight = table.Column<int>(type: "int", nullable: false),
                    StockRemaining = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionRewards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromotionVoucherEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionVoucherEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionVoucherEvents_PromotionVouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "PromotionVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromotionSpins",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SpinDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RewardId = table.Column<int>(type: "int", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionSpins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionSpins_PromotionRewards_RewardId",
                        column: x => x.RewardId,
                        principalTable: "PromotionRewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromotionSpins_PromotionVouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "PromotionVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionVouchers_RewardId",
                table: "PromotionVouchers",
                column: "RewardId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderGiftItems_OrderId",
                table: "OrderGiftItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderGiftItems_PromotionVoucherId",
                table: "OrderGiftItems",
                column: "PromotionVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRewards_TemplateCode",
                table: "PromotionRewards",
                column: "TemplateCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionSpins_RewardId",
                table: "PromotionSpins",
                column: "RewardId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionSpins_UserName_SpinDate",
                table: "PromotionSpins",
                columns: new[] { "UserName", "SpinDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionSpins_VoucherId",
                table: "PromotionSpins",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionVoucherEvents_VoucherId_CreatedAt",
                table: "PromotionVoucherEvents",
                columns: new[] { "VoucherId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_PromotionVouchers_PromotionRewards_RewardId",
                table: "PromotionVouchers",
                column: "RewardId",
                principalTable: "PromotionRewards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PromotionVouchers_PromotionRewards_RewardId",
                table: "PromotionVouchers");

            migrationBuilder.DropTable(
                name: "OrderGiftItems");

            migrationBuilder.DropTable(
                name: "PromotionSpins");

            migrationBuilder.DropTable(
                name: "PromotionVoucherEvents");

            migrationBuilder.DropTable(
                name: "PromotionRewards");

            migrationBuilder.DropIndex(
                name: "IX_PromotionVouchers_RewardId",
                table: "PromotionVouchers");

            migrationBuilder.DropColumn(
                name: "RewardId",
                table: "PromotionVouchers");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "PromotionTemplateCode",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "ShippingFee",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Oders");
        }
    }
}
