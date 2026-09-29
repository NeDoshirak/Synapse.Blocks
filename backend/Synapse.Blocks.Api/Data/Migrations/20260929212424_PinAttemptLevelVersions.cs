using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synapse.Blocks.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PinAttemptLevelVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LevelVersionIdsJson",
                table: "attempts",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.Sql("UPDATE attempts SET \"LevelVersionIdsJson\" = COALESCE((SELECT jsonb_agg(\"LevelVersionId\" ORDER BY \"Order\") FROM case_levels WHERE \"CaseId\" = attempts.\"CaseId\"), '[]'::jsonb)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LevelVersionIdsJson",
                table: "attempts");
        }
    }
}
