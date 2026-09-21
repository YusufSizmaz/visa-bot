using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisaTelegramBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ParsingRules = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FetchInterval = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextFetchAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastFetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSucceededAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsecutiveFailureCount = table.Column<int>(type: "int", nullable: false),
                    LastFetchError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsSources", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                });

            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ContentHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryStatus = table.Column<int>(type: "int", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "int", nullable: false),
                    NextDeliveryAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryLockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastDeliveryError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_NewsItems_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "CIX_NewsItems_DiscoveredAtUtc_Id",
                table: "NewsItems",
                columns: new[] { "DiscoveredAtUtc", "Id" },
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_DeliveryStatus_DiscoveredAtUtc",
                table: "NewsItems",
                columns: new[] { "DeliveryStatus", "DiscoveredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_NewsSourceId_DiscoveredAtUtc",
                table: "NewsItems",
                columns: new[] { "NewsSourceId", "DiscoveredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_Pending",
                table: "NewsItems",
                column: "NextDeliveryAttemptAtUtc",
                filter: "[DeliveryStatus] = 1")
                .Annotation("SqlServer:Include", new[] { "DeliveryLockedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_NewsItems_ContentHash",
                table: "NewsItems",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "CIX_NewsSources_CreatedAtUtc_Id",
                table: "NewsSources",
                columns: new[] { "CreatedAtUtc", "Id" },
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsSources_IsActive_NextFetchAtUtc",
                table: "NewsSources",
                columns: new[] { "IsActive", "NextFetchAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsItems");

            migrationBuilder.DropTable(
                name: "NewsSources");
        }
    }
}
