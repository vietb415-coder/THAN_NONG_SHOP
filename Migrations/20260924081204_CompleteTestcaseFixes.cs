using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace THAN_NONG_SHOP.Migrations
{
    /// <inheritdoc />
    public partial class CompleteTestcaseFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS(SELECT 1 FROM Users GROUP BY UPPER(LTRIM(RTRIM(Email))) HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM Users GROUP BY LTRIM(RTRIM(Phone)) HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM Users GROUP BY UserName HAVING COUNT(*)>1)
 THROW 51000, 'Duplicate username/email/phone. Resolve duplicates before migration; no accounts were deleted.', 1;
IF EXISTS(SELECT 1 FROM Users WHERE LEN(UserName)>100 OR LEN(Phone)>20 OR LEN(Fullname)>100 OR LEN(Email)>254)
 OR EXISTS(SELECT 1 FROM Products WHERE LEN(Name)>200 OR stockQuantity<0)
 THROW 51001, 'Legacy data exceeds new limits. Correct the data before migration.', 1;
UPDATE Users SET Phone=LTRIM(RTRIM(Phone)),Email=LTRIM(RTRIM(Email));
");
            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Fullname",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmationExpiresAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationTokenHash",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SellerApproved",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SellerRequested",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Products",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Certification",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerUserName",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Products",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutToken",
                table: "Oders",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Oders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InventoryRestored",
                table: "Oders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentExpiresAt",
                table: "Oders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Oders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "PaymentNeedsReview",
                table: "Oders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "Oders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentUrl",
                table: "Oders",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingMethod",
                table: "Oders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Oders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentStatus",
                table: "OderDetails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SellerUserName",
                table: "OderDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Users",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                computedColumnSql: "UPPER(LTRIM(RTRIM([Email])))",
                stored: true);

            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Recipient = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    HarvestDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemainingQuantity = table.Column<int>(type: "int", nullable: false),
                    IsLegacy = table.Column<bool>(type: "bit", nullable: false),
                    IsNearExpiry = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBatches_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductReviewId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsVideo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewMedia_ProductReviews_ProductReviewId",
                        column: x => x.ProductReviewId,
                        principalTable: "ProductReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedCartItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedCartItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedCartItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BatchAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderDetailId = table.Column<int>(type: "int", nullable: false),
                    ProductBatchId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatchAllocations_OderDetails_OrderDetailId",
                        column: x => x.OrderDetailId,
                        principalTable: "OderDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BatchAllocations_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Phone",
                table: "Users",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Oders_CheckoutToken",
                table: "Oders",
                column: "CheckoutToken",
                unique: true,
                filter: "[CheckoutToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Oders_PaymentReference",
                table: "Oders",
                column: "PaymentReference",
                unique: true,
                filter: "[PaymentReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BatchAllocations_OrderDetailId",
                table: "BatchAllocations",
                column: "OrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchAllocations_ProductBatchId",
                table: "BatchAllocations",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_SentAt_NextAttemptAt",
                table: "EmailMessages",
                columns: new[] { "SentAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_Code",
                table: "ProductBatches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductId",
                table: "ProductBatches",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewMedia_ProductReviewId",
                table: "ReviewMedia",
                column: "ProductReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedCartItems_ProductId",
                table: "SavedCartItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedCartItems_UserName_ProductId",
                table: "SavedCartItems",
                columns: new[] { "UserName", "ProductId" },
                unique: true);
            migrationBuilder.Sql(@"
UPDATE Users SET EmailConfirmed=1;
UPDATE Products SET Unit=N'kg' WHERE Unit=N'';
UPDATE Oders SET Status=N'Chờ xác nhận' WHERE Status IN (N'Chờ xử lý',N'Đang chờ xử lý');
UPDATE Oders SET Status=N'Đã giao',CompletedAt=OrderDate WHERE Status=N'Đã hoàn thành';
UPDATE Oders SET PaymentMethod=CASE WHEN PayOSOrderCode IS NULL THEN 'cod' ELSE 'payos' END,ShippingMethod='standard',InventoryRestored=CASE WHEN Status=N'Đã hủy' THEN 1 ELSE 0 END;
UPDATE Oders SET PaymentExpiresAt=DATEADD(minute,15,DATEADD(hour,-7,OrderDate)) WHERE Status=N'Chờ thanh toán';
UPDATE d SET FulfillmentStatus=CASE WHEN o.Status IN(N'Đang đóng gói',N'Đang giao',N'Đã giao',N'Đã hủy') THEN o.Status ELSE N'Chờ xác nhận' END FROM OderDetails d INNER JOIN Oders o ON o.Id=d.OderId;
INSERT INTO ProductBatches(ProductId,Code,RemainingQuantity,IsLegacy,IsNearExpiry) SELECT Id,CONCAT('LEGACY-',Id),stockQuantity,1,0 FROM Products;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchAllocations");

            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropTable(
                name: "ReviewMedia");

            migrationBuilder.DropTable(
                name: "SavedCartItems");

            migrationBuilder.DropTable(
                name: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Phone",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_UserName",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Oders_CheckoutToken",
                table: "Oders");

            migrationBuilder.DropIndex(
                name: "IX_Oders_PaymentReference",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ConfirmationExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ConfirmationTokenHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SellerApproved",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SellerRequested",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Certification",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SellerUserName",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CheckoutToken",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "InventoryRestored",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "PaymentExpiresAt",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "PaymentNeedsReview",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "PaymentUrl",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "ShippingMethod",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Oders");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                table: "OderDetails");

            migrationBuilder.DropColumn(
                name: "SellerUserName",
                table: "OderDetails");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Fullname",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }
    }
}
