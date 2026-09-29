using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Synapse.Blocks.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudentAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ContinuationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_participants_teacher_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "teacher_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attempts_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attempts_teacher_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "teacher_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attempt_level_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attempt_level_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attempt_level_results_attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attempt_level_results_level_versions_LevelVersionId",
                        column: x => x.LevelVersionId,
                        principalTable: "level_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attempt_level_results_AttemptId_LevelVersionId",
                table: "attempt_level_results",
                columns: new[] { "AttemptId", "LevelVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attempt_level_results_LevelVersionId",
                table: "attempt_level_results",
                column: "LevelVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_attempts_CaseId_ParticipantId_StartedAt",
                table: "attempts",
                columns: new[] { "CaseId", "ParticipantId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_attempts_ParticipantId",
                table: "attempts",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_participants_CaseId_ContinuationHash",
                table: "participants",
                columns: new[] { "CaseId", "ContinuationHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participants_CaseId_NormalizedName",
                table: "participants",
                columns: new[] { "CaseId", "NormalizedName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attempt_level_results");

            migrationBuilder.DropTable(
                name: "attempts");

            migrationBuilder.DropTable(
                name: "participants");
        }
    }
}
