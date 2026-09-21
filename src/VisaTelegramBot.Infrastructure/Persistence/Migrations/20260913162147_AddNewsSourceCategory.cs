using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisaTelegramBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsSourceCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "NewsSources",
                type: "int",
                nullable: false,
                // Mevcut kaynaklarin hepsi vize kaynagi (SourceCategory.Visa = 1).
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "NewsSources");
        }
    }
}
