using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsTestToChampionship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTest",
                table: "Championships",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTest",
                table: "Championships");
        }
    }
}
