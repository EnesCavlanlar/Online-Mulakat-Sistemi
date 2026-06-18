using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DenemeTest.Migrations
{
    /// <inheritdoc />
    public partial class AddExamRecordingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CancelReason",
                table: "AppExamSessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AppExamRecordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "video/webm"),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsStorageDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    StorageDeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExamRecordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppExamRecordings_AppExamSessions_ExamSessionId",
                        column: x => x.ExamSessionId,
                        principalTable: "AppExamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppScores_ExamSessionId",
                table: "AppScores",
                column: "ExamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppProctoringEvents_ExamSessionId",
                table: "AppProctoringEvents",
                column: "ExamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSessions_CandidateId",
                table: "AppExamSessions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSessions_FinishedAt",
                table: "AppExamSessions",
                column: "FinishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSessions_StartedAt",
                table: "AppExamSessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSessions_TestId",
                table: "AppExamSessions",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAnswers_ExamSessionId",
                table: "AppAnswers",
                column: "ExamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAnswers_ExamSessionId_QuestionId",
                table: "AppAnswers",
                columns: new[] { "ExamSessionId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAnswers_QuestionId",
                table: "AppAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamRecordings_ExamSessionId",
                table: "AppExamRecordings",
                column: "ExamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamRecordings_ExamSessionId_Kind",
                table: "AppExamRecordings",
                columns: new[] { "ExamSessionId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamRecordings_ExpiresAt",
                table: "AppExamRecordings",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamRecordings_IsStorageDeleted",
                table: "AppExamRecordings",
                column: "IsStorageDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppExamRecordings");

            migrationBuilder.DropIndex(
                name: "IX_AppScores_ExamSessionId",
                table: "AppScores");

            migrationBuilder.DropIndex(
                name: "IX_AppProctoringEvents_ExamSessionId",
                table: "AppProctoringEvents");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSessions_CandidateId",
                table: "AppExamSessions");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSessions_FinishedAt",
                table: "AppExamSessions");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSessions_StartedAt",
                table: "AppExamSessions");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSessions_TestId",
                table: "AppExamSessions");

            migrationBuilder.DropIndex(
                name: "IX_AppAnswers_ExamSessionId",
                table: "AppAnswers");

            migrationBuilder.DropIndex(
                name: "IX_AppAnswers_ExamSessionId_QuestionId",
                table: "AppAnswers");

            migrationBuilder.DropIndex(
                name: "IX_AppAnswers_QuestionId",
                table: "AppAnswers");

            migrationBuilder.AlterColumn<string>(
                name: "CancelReason",
                table: "AppExamSessions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
