using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecretSantaRuleAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SecretSantaRuleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserNickname = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RuleText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecretSantaRuleAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecretSantaRuleAssignments_RuleText",
                table: "SecretSantaRuleAssignments",
                column: "RuleText",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecretSantaRuleAssignments_UserId",
                table: "SecretSantaRuleAssignments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecretSantaRuleAssignments");
        }
    }
}
