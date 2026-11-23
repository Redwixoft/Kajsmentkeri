using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPercentagePredictionRecallAndOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PercentagePredictionOutcomes",
                columns: table => new
                {
                    QuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    Happened = table.Column<bool>(type: "boolean", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PercentagePredictionOutcomes", x => x.QuestionNumber);
                });

            migrationBuilder.CreateTable(
                name: "PercentagePredictionRecalls",
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
                    table.PrimaryKey("PK_PercentagePredictionRecalls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PercentagePredictionRecalls_UserId",
                table: "PercentagePredictionRecalls",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PercentagePredictionOutcomes");

            migrationBuilder.DropTable(
                name: "PercentagePredictionRecalls");
        }
    }
}
