using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synapse.Blocks.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaseSectionsAndAttemptPresentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_case_sections_teacher_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "teacher_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "SectionId",
                table: "case_levels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayTitle",
                table: "case_levels",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO case_sections ("Id", "CaseId", "Title", "Order")
                SELECT gen_random_uuid(), "Id", NULL, 0
                FROM teacher_cases
                """);

            migrationBuilder.Sql("""
                UPDATE case_levels AS assignment
                SET "SectionId" = section."Id"
                FROM case_sections AS section
                WHERE section."CaseId" = assignment."CaseId"
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "SectionId",
                table: "case_levels",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LevelPresentationJson",
                table: "attempts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.DropIndex(
                name: "IX_case_levels_CaseId_Order",
                table: "case_levels");

            migrationBuilder.CreateIndex(
                name: "IX_case_levels_SectionId_Order",
                table: "case_levels",
                columns: new[] { "SectionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_case_levels_CaseId",
                table: "case_levels",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_case_sections_CaseId",
                table: "case_sections",
                column: "CaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_case_levels_case_sections_SectionId",
                table: "case_levels",
                column: "SectionId",
                principalTable: "case_sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_case_levels_SectionId_Order",
                table: "case_levels");

            migrationBuilder.DropIndex(
                name: "IX_case_levels_CaseId",
                table: "case_levels");

            migrationBuilder.Sql("""
                WITH flattened AS (
                    SELECT assignment."Id",
                           ROW_NUMBER() OVER (PARTITION BY assignment."CaseId" ORDER BY section."Order", assignment."Order") - 1 AS "Order"
                    FROM case_levels AS assignment
                    INNER JOIN case_sections AS section ON section."Id" = assignment."SectionId"
                )
                UPDATE case_levels AS assignment
                SET "Order" = flattened."Order"
                FROM flattened
                WHERE assignment."Id" = flattened."Id"
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_case_levels_case_sections_SectionId",
                table: "case_levels");

            migrationBuilder.DropIndex(
                name: "IX_case_sections_CaseId",
                table: "case_sections");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "case_levels");

            migrationBuilder.DropColumn(
                name: "DisplayTitle",
                table: "case_levels");

            migrationBuilder.DropColumn(
                name: "LevelPresentationJson",
                table: "attempts");

            migrationBuilder.DropTable(name: "case_sections");

            migrationBuilder.CreateIndex(
                name: "IX_case_levels_CaseId_Order",
                table: "case_levels",
                columns: new[] { "CaseId", "Order" },
                unique: true);
        }
    }
}
