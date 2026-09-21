using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisaTelegramBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsSourceKeywordFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KeywordFilter",
                table: "NewsSources",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeywordFilter",
                table: "NewsSources");
        }
    }
}
