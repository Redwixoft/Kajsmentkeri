using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictionAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PredictionAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminName = table.Column<string>(type: "text", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserName = table.Column<string>(type: "text", nullable: false),
                    OldHomeScore = table.Column<int>(type: "integer", nullable: true),
                    OldAwayScore = table.Column<int>(type: "integer", nullable: true),
                    NewHomeScore = table.Column<int>(type: "integer", nullable: false),
                    NewAwayScore = table.Column<int>(type: "integer", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MatchSummary = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionAuditLogs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PredictionAuditLogs");
        }
    }
}
