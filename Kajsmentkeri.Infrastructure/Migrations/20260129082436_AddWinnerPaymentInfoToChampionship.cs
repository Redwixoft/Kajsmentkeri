using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWinnerPaymentInfoToChampionship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WinnerIban",
                table: "Championships",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WinnerNote",
                table: "Championships",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WinnerIban",
                table: "Championships");

            migrationBuilder.DropColumn(
                name: "WinnerNote",
                table: "Championships");
        }
    }
}
