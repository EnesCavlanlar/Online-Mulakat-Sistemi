using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DenemeTest.Migrations
{
    /// <inheritdoc />
    public partial class AddViolationCountToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ViolationCount",
                table: "AppExamSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ViolationCount",
                table: "AppExamSessions");
        }
    }
}
