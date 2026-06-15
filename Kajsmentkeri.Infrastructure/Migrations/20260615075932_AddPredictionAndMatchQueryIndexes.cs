using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictionAndMatchQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_ChampionshipId",
                table: "Matches");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ChampionshipId_StartTimeUtc",
                table: "Matches",
                columns: new[] { "ChampionshipId", "StartTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_ChampionshipId_StartTimeUtc",
                table: "Matches");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ChampionshipId",
                table: "Matches",
                column: "ChampionshipId");
        }
    }
}
