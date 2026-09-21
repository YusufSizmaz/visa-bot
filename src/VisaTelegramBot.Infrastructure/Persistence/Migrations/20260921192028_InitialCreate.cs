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
                name: "ChannelMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "character varying(3500)", maxLength: 3500, nullable: false),
                    LinkUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ButtonText = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ButtonUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PhotoContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    PhotoContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PhotoFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ParsingRules = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    KeywordFilter = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TurkishOnly = table.Column<bool>(type: "boolean", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    FetchInterval = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextFetchAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastFetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSucceededAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsecutiveFailureCount = table.Column<int>(type: "integer", nullable: false),
                    LastFetchError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryStatus = table.Column<int>(type: "integer", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextDeliveryAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryLockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastDeliveryError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ArchiveReason = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsItems_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_CreatedAtUtc_Id",
                table: "ChannelMessages",
                columns: new[] { "CreatedAtUtc", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_Scheduled",
                table: "ChannelMessages",
                column: "ScheduledAtUtc",
                filter: "\"Status\" = 1")
                .Annotation("Npgsql:IndexInclude", new[] { "LockedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_DeliveryStatus_DiscoveredAtUtc",
                table: "NewsItems",
                columns: new[] { "DeliveryStatus", "DiscoveredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_DiscoveredAtUtc_Id",
                table: "NewsItems",
                columns: new[] { "DiscoveredAtUtc", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_NewsSourceId_DiscoveredAtUtc",
                table: "NewsItems",
                columns: new[] { "NewsSourceId", "DiscoveredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_Pending",
                table: "NewsItems",
                column: "NextDeliveryAttemptAtUtc",
                filter: "\"DeliveryStatus\" = 1")
                .Annotation("Npgsql:IndexInclude", new[] { "DeliveryLockedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_NewsItems_ContentHash",
                table: "NewsItems",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsSources_CreatedAtUtc_Id",
                table: "NewsSources",
                columns: new[] { "CreatedAtUtc", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsSources_IsActive_NextFetchAtUtc",
                table: "NewsSources",
                columns: new[] { "IsActive", "NextFetchAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelMessages");

            migrationBuilder.DropTable(
                name: "NewsItems");

            migrationBuilder.DropTable(
                name: "NewsSources");
        }
    }
}
