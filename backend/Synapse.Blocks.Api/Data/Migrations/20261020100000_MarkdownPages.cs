using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synapse.Blocks.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MarkdownPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "markdown_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_markdown_assets", x => x.Id);
                    table.CheckConstraint("CK_markdown_assets_length", "\"Length\" > 0 AND \"Length\" <= 10485760");
                    table.ForeignKey(
                        name: "FK_markdown_assets_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "markdown_page_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Markdown = table.Column<string>(type: "character varying(102400)", maxLength: 102400, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_markdown_page_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "markdown_pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false),
                    BuiltInKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_markdown_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_markdown_pages_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_markdown_pages_markdown_page_versions_CurrentVersionId",
                        column: x => x.CurrentVersionId,
                        principalTable: "markdown_page_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_markdown_assets_OwnerId",
                table: "markdown_assets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_markdown_page_versions_PageId_Version",
                table: "markdown_page_versions",
                columns: new[] { "PageId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_markdown_pages_BuiltInKey",
                table: "markdown_pages",
                column: "BuiltInKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_markdown_pages_CurrentVersionId",
                table: "markdown_pages",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_markdown_pages_OwnerId_IsArchived_Title",
                table: "markdown_pages",
                columns: new[] { "OwnerId", "IsArchived", "Title" });

            migrationBuilder.AddForeignKey(
                name: "FK_markdown_page_versions_markdown_pages_PageId",
                table: "markdown_page_versions",
                column: "PageId",
                principalTable: "markdown_pages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_markdown_page_versions_markdown_pages_PageId",
                table: "markdown_page_versions");

            migrationBuilder.DropTable(
                name: "markdown_assets");

            migrationBuilder.DropTable(
                name: "markdown_pages");

            migrationBuilder.DropTable(
                name: "markdown_page_versions");
        }
    }
}
