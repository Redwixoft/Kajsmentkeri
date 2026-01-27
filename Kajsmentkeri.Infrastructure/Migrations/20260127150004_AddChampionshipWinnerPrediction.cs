using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChampionshipWinnerPrediction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointsForChampionshipRunnerUp",
                table: "ChampionshipScoringRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsForChampionshipThirdPlace",
                table: "ChampionshipScoringRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointsForChampionshipWinner",
                table: "ChampionshipScoringRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsChampionshipEnded",
                table: "Championships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsChampionshipWinnerPrediction",
                table: "Championships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ChampionshipWinnerPredictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChampionshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamName = table.Column<string>(type: "text", nullable: false),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChampionshipWinnerPredictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChampionshipWinnerPredictions_Championships_ChampionshipId",
                        column: x => x.ChampionshipId,
                        principalTable: "Championships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChampionshipWinnerPredictions_ChampionshipId",
                table: "ChampionshipWinnerPredictions",
                column: "ChampionshipId");

            migrationBuilder.CreateIndex(
                name: "IX_ChampionshipWinnerPredictions_UserId_ChampionshipId",
                table: "ChampionshipWinnerPredictions",
                columns: new[] { "UserId", "ChampionshipId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChampionshipWinnerPredictions");

            migrationBuilder.DropColumn(
                name: "PointsForChampionshipRunnerUp",
                table: "ChampionshipScoringRules");

            migrationBuilder.DropColumn(
                name: "PointsForChampionshipThirdPlace",
                table: "ChampionshipScoringRules");

            migrationBuilder.DropColumn(
                name: "PointsForChampionshipWinner",
                table: "ChampionshipScoringRules");

            migrationBuilder.DropColumn(
                name: "IsChampionshipEnded",
                table: "Championships");

            migrationBuilder.DropColumn(
                name: "SupportsChampionshipWinnerPrediction",
                table: "Championships");
        }
    }
}
