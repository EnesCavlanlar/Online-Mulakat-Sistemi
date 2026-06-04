using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DenemeTest.Migrations
{
    /// <inheritdoc />
    public partial class FixInvitationTokenHashColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppExamInvitations_Token",
                table: "AppExamInvitations");

            migrationBuilder.RenameColumn(
                name: "Token",
                table: "AppExamInvitations",
                newName: "TokenHash");

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "AppExamInvitations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamInvitations_TokenHash",
                table: "AppExamInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppExamInvitations_TokenHash",
                table: "AppExamInvitations");

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "AppExamInvitations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.RenameColumn(
                name: "TokenHash",
                table: "AppExamInvitations",
                newName: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamInvitations_Token",
                table: "AppExamInvitations",
                column: "Token",
                unique: true);
        }
    }
}