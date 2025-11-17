using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DenemeTest.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsUsed",
                table: "AppExamInvitations",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "AppExamInvitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UsedAt",
                table: "AppExamInvitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AppCandidates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamInvitations_CandidateId_TestId",
                table: "AppExamInvitations",
                columns: new[] { "CandidateId", "TestId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppExamInvitations_TestId",
                table: "AppExamInvitations",
                column: "TestId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppExamInvitations_AppCandidates_CandidateId",
                table: "AppExamInvitations",
                column: "CandidateId",
                principalTable: "AppCandidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppExamInvitations_AppTests_TestId",
                table: "AppExamInvitations",
                column: "TestId",
                principalTable: "AppTests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppExamInvitations_AppCandidates_CandidateId",
                table: "AppExamInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_AppExamInvitations_AppTests_TestId",
                table: "AppExamInvitations");

            migrationBuilder.DropIndex(
                name: "IX_AppExamInvitations_CandidateId_TestId",
                table: "AppExamInvitations");

            migrationBuilder.DropIndex(
                name: "IX_AppExamInvitations_TestId",
                table: "AppExamInvitations");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "AppExamInvitations");

            migrationBuilder.DropColumn(
                name: "UsedAt",
                table: "AppExamInvitations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppCandidates");

            migrationBuilder.AlterColumn<bool>(
                name: "IsUsed",
                table: "AppExamInvitations",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
