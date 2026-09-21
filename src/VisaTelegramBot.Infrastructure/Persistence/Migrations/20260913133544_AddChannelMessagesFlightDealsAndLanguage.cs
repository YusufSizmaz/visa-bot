using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisaTelegramBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelMessagesFlightDealsAndLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TurkishOnly",
                table: "NewsSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveReason",
                table: "NewsItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChannelMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(3500)", maxLength: 3500, nullable: false),
                    LinkUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ButtonText = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ButtonUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    PhotoContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PhotoContentType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    PhotoFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMessages", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                });

            migrationBuilder.CreateTable(
                name: "FlightRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origin = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Destination = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaxPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    MonthsAhead = table.Column<int>(type: "int", nullable: false),
                    CheckInterval = table.Column<long>(type: "bigint", nullable: false),
                    AutoPublish = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextCheckAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightRoutes", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                });

            migrationBuilder.CreateTable(
                name: "FlightDeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlightRouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origin = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Destination = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    DepartureAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Airline = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FlightNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Transfers = table.Column<int>(type: "int", nullable: false),
                    BookingUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    DealKey = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    FoundAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChannelMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightDeals", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_FlightDeals_ChannelMessages_ChannelMessageId",
                        column: x => x.ChannelMessageId,
                        principalTable: "ChannelMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FlightDeals_FlightRoutes_FlightRouteId",
                        column: x => x.FlightRouteId,
                        principalTable: "FlightRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "CIX_ChannelMessages_CreatedAtUtc_Id",
                table: "ChannelMessages",
                columns: new[] { "CreatedAtUtc", "Id" },
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_Scheduled",
                table: "ChannelMessages",
                column: "ScheduledAtUtc",
                filter: "[Status] = 1")
                .Annotation("SqlServer:Include", new[] { "LockedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "CIX_FlightDeals_FoundAtUtc_Id",
                table: "FlightDeals",
                columns: new[] { "FoundAtUtc", "Id" },
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_FlightDeals_ChannelMessageId",
                table: "FlightDeals",
                column: "ChannelMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightDeals_FlightRouteId",
                table: "FlightDeals",
                column: "FlightRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightDeals_Status_FoundAtUtc",
                table: "FlightDeals",
                columns: new[] { "Status", "FoundAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_FlightDeals_DealKey",
                table: "FlightDeals",
                column: "DealKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "CIX_FlightRoutes_CreatedAtUtc_Id",
                table: "FlightRoutes",
                columns: new[] { "CreatedAtUtc", "Id" },
                unique: true)
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_FlightRoutes_IsActive_NextCheckAtUtc",
                table: "FlightRoutes",
                columns: new[] { "IsActive", "NextCheckAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlightDeals");

            migrationBuilder.DropTable(
                name: "ChannelMessages");

            migrationBuilder.DropTable(
                name: "FlightRoutes");

            migrationBuilder.DropColumn(
                name: "TurkishOnly",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "NewsItems");
        }
    }
}
