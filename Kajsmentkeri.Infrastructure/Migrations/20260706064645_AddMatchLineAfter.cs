using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchLineAfter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LineAfter",
                table: "Matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineAfter",
                table: "Matches");
        }
    }
}
