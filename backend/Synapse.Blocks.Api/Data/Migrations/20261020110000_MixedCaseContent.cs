using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synapse.Blocks.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MixedCaseContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProgressionMode",
                table: "teacher_cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sequential");

            migrationBuilder.CreateTable(
                name: "case_content_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LevelVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarkdownPageVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    DisplayTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_content_items", x => x.Id);
                    table.CheckConstraint("CK_case_content_items_kind_reference", "(\"Kind\" = 'level' AND \"LevelVersionId\" IS NOT NULL AND \"MarkdownPageVersionId\" IS NULL) OR (\"Kind\" = 'markdown' AND \"LevelVersionId\" IS NULL AND \"MarkdownPageVersionId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_case_content_items_case_sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "case_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_case_content_items_level_versions_LevelVersionId",
                        column: x => x.LevelVersionId,
                        principalTable: "level_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_case_content_items_markdown_page_versions_MarkdownPageVersi~",
                        column: x => x.MarkdownPageVersionId,
                        principalTable: "markdown_page_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_case_content_items_teacher_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "teacher_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO case_content_items
                    ("Id", "CaseId", "SectionId", "Kind", "LevelVersionId", "MarkdownPageVersionId", "Order", "DisplayTitle")
                SELECT "Id", "CaseId", "SectionId", 'level', "LevelVersionId", NULL, "Order", "DisplayTitle"
                FROM case_levels
                """);

            migrationBuilder.DropTable(
                name: "case_levels");

            migrationBuilder.CreateIndex(
                name: "IX_case_content_items_CaseId",
                table: "case_content_items",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_case_content_items_CaseId_LevelVersionId",
                table: "case_content_items",
                columns: new[] { "CaseId", "LevelVersionId" },
                unique: true,
                filter: "\"Kind\" = 'level'");

            migrationBuilder.CreateIndex(
                name: "IX_case_content_items_LevelVersionId",
                table: "case_content_items",
                column: "LevelVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_case_content_items_MarkdownPageVersionId",
                table: "case_content_items",
                column: "MarkdownPageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_case_content_items_SectionId_Order",
                table: "case_content_items",
                columns: new[] { "SectionId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_levels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_levels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_case_levels_case_sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "case_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_case_levels_level_versions_LevelVersionId",
                        column: x => x.LevelVersionId,
                        principalTable: "level_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_case_levels_teacher_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "teacher_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_case_levels_CaseId",
                table: "case_levels",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_case_levels_LevelVersionId",
                table: "case_levels",
                column: "LevelVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_case_levels_SectionId_Order",
                table: "case_levels",
                columns: new[] { "SectionId", "Order" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO case_levels
                    ("Id", "CaseId", "SectionId", "LevelId", "LevelVersionId", "Order", "DisplayTitle")
                SELECT item."Id", item."CaseId", item."SectionId", version."LevelId", item."LevelVersionId", item."Order", item."DisplayTitle"
                FROM case_content_items AS item
                INNER JOIN level_versions AS version ON version."Id" = item."LevelVersionId"
                WHERE item."Kind" = 'level'
                """);

            migrationBuilder.DropTable(
                name: "case_content_items");

            migrationBuilder.DropColumn(
                name: "ProgressionMode",
                table: "teacher_cases");
        }
    }
}
