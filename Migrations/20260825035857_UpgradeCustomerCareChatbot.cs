using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using THAN_NONG_SHOP.Data;

#nullable disable

namespace THAN_NONG_SHOP.Migrations;

[DbContext(typeof(THAN_NONG_SHOP_DbContext))]
[Migration("20260825035857_UpgradeCustomerCareChatbot")]
public partial class UpgradeCustomerCareChatbot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChatConversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                VisitorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                NeedsHuman = table.Column<bool>(type: "bit", nullable: false),
                HandoffSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ChatConversations", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ChatKnowledge",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ChatKnowledge", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ChatMessages",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Helpful = table.Column<bool>(type: "bit", nullable: true),
                IsAi = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessages", x => x.Id);
                table.ForeignKey("FK_ChatMessages_ChatConversations_ConversationId", x => x.ConversationId,
                    "ChatConversations", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_ChatConversations_LastMessageAt", "ChatConversations", "LastMessageAt");
        migrationBuilder.CreateIndex("IX_ChatMessages_ConversationId_CreatedAt", "ChatMessages", new[] { "ConversationId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ChatKnowledge");
        migrationBuilder.DropTable(name: "ChatMessages");
        migrationBuilder.DropTable(name: "ChatConversations");
    }
}
