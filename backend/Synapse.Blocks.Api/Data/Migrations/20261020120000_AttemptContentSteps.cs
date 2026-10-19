using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synapse.Blocks.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttemptContentSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProgressionMode",
                table: "attempts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sequential");

            migrationBuilder.CreateTable(
                name: "attempt_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SectionOrder = table.Column<int>(type: "integer", nullable: true),
                    SectionTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LevelVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarkdownPageVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Markdown = table.Column<string>(type: "character varying(102400)", maxLength: 102400, nullable: true),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attempt_steps", x => x.Id);
                    table.CheckConstraint("CK_attempt_steps_kind_reference", "(\"Kind\" = 'level' AND \"LevelVersionId\" IS NOT NULL AND \"MarkdownPageVersionId\" IS NULL AND \"Markdown\" IS NULL) OR (\"Kind\" = 'markdown' AND \"LevelVersionId\" IS NULL AND \"MarkdownPageVersionId\" IS NOT NULL AND \"Markdown\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_attempt_steps_attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attempt_steps_level_versions_LevelVersionId",
                        column: x => x.LevelVersionId,
                        principalTable: "level_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attempt_steps_markdown_page_versions_MarkdownPageVersionId",
                        column: x => x.MarkdownPageVersionId,
                        principalTable: "markdown_page_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attempt_steps_AttemptId_Order",
                table: "attempt_steps",
                columns: new[] { "AttemptId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attempt_steps_AttemptId_SourceItemId",
                table: "attempt_steps",
                columns: new[] { "AttemptId", "SourceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attempt_steps_LevelVersionId",
                table: "attempt_steps",
                column: "LevelVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_attempt_steps_MarkdownPageVersionId",
                table: "attempt_steps",
                column: "MarkdownPageVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attempt_steps");

            migrationBuilder.DropColumn(
                name: "ProgressionMode",
                table: "attempts");
        }
    }
}
