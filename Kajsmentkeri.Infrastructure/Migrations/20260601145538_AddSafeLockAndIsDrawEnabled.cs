using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSafeLockAndIsDrawEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSafeLockTrigger",
                table: "PredictionAuditLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDrawEnabled",
                table: "Championships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SafeLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeWinPredictedHome = table.Column<int>(type: "integer", nullable: false),
                    HomeWinPredictedAway = table.Column<int>(type: "integer", nullable: false),
                    DrawPredictedHome = table.Column<int>(type: "integer", nullable: true),
                    DrawPredictedAway = table.Column<int>(type: "integer", nullable: true),
                    AwayWinPredictedHome = table.Column<int>(type: "integer", nullable: false),
                    AwayWinPredictedAway = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafeLocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafeLocks_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SafeLocks_MatchId",
                table: "SafeLocks",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SafeLocks_OwnerUserId_MatchId",
                table: "SafeLocks",
                columns: new[] { "OwnerUserId", "MatchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SafeLocks");

            migrationBuilder.DropColumn(
                name: "IsSafeLockTrigger",
                table: "PredictionAuditLogs");

            migrationBuilder.DropColumn(
                name: "IsDrawEnabled",
                table: "Championships");
        }
    }
}
