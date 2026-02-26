using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictionAuditLogIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PredictionAuditLogs_MatchId",
                table: "PredictionAuditLogs",
                column: "MatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PredictionAuditLogs_MatchId",
                table: "PredictionAuditLogs");
        }
    }
}
