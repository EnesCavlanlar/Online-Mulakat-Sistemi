using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DenemeTest.Migrations
{
    /// <inheritdoc />
    public partial class Fix_Create_CodeReviews_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCodeReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),

                    ExamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),

                    TestsPassed = table.Column<bool>(type: "boolean", nullable: false),
                    PassedCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),

                    IsSuspicious = table.Column<bool>(type: "boolean", nullable: false),
                    QualityScore = table.Column<int>(type: "integer", nullable: true),

                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Flags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),

                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),

                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),

                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),

                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCodeReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCodeReviews_ExamSessionId",
                table: "AppCodeReviews",
                column: "ExamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCodeReviews_QuestionId",
                table: "AppCodeReviews",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCodeReviews_ExamSessionId_QuestionId",
                table: "AppCodeReviews",
                columns: new[] { "ExamSessionId", "QuestionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCodeReviews");
        }
    }
}