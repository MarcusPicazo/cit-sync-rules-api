using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CitSync.Rules.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRuleSetsAuditAndRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "rules",
                table: "Reservations",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                schema: "rules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Entity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Before = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    After = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleSets",
                schema: "rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScopeKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActiveVersionNumber = table.Column<int>(type: "int", nullable: true),
                    LatestVersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleSetVersions",
                schema: "rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReviewedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActivatedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleSetVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleSetVersions_RuleSets_RuleSetId",
                        column: x => x.RuleSetId,
                        principalSchema: "rules",
                        principalTable: "RuleSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RuleSetVersionRules",
                schema: "rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RuleSetVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleSetVersionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleSetVersionRules_RuleSetVersions_RuleSetVersionId",
                        column: x => x.RuleSetVersionId,
                        principalSchema: "rules",
                        principalTable: "RuleSetVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Entity_EntityId_TimestampUtc",
                schema: "rules",
                table: "AuditEntries",
                columns: new[] { "Entity", "EntityId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RuleSets_Name",
                schema: "rules",
                table: "RuleSets",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleSets_Scope_ScopeKey",
                schema: "rules",
                table: "RuleSets",
                columns: new[] { "Scope", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleSetVersionRules_RuleSetVersionId",
                schema: "rules",
                table: "RuleSetVersionRules",
                column: "RuleSetVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleSetVersions_RuleSetId_Number",
                schema: "rules",
                table: "RuleSetVersions",
                columns: new[] { "RuleSetId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries",
                schema: "rules");

            migrationBuilder.DropTable(
                name: "RuleSetVersionRules",
                schema: "rules");

            migrationBuilder.DropTable(
                name: "RuleSetVersions",
                schema: "rules");

            migrationBuilder.DropTable(
                name: "RuleSets",
                schema: "rules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "rules",
                table: "Reservations");
        }
    }
}
