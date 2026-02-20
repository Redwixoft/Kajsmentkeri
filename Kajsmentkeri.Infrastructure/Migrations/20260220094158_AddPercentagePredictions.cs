using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPercentagePredictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PercentagePredictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Question1 = table.Column<int>(type: "integer", nullable: false),
                    Question2 = table.Column<int>(type: "integer", nullable: false),
                    Question3 = table.Column<int>(type: "integer", nullable: false),
                    Question4 = table.Column<int>(type: "integer", nullable: false),
                    Question5 = table.Column<int>(type: "integer", nullable: false),
                    Question6 = table.Column<int>(type: "integer", nullable: false),
                    Question7 = table.Column<int>(type: "integer", nullable: false),
                    Question8 = table.Column<int>(type: "integer", nullable: false),
                    Question9 = table.Column<int>(type: "integer", nullable: false),
                    Question10 = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PercentagePredictions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PercentagePredictions_UserId",
                table: "PercentagePredictions",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PercentagePredictions");
        }
    }
}
