using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DenemeTest.Migrations
{
    /// <inheritdoc />
    public partial class Add_Status_To_Candidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "AppTests",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<double>(
                name: "PassScore",
                table: "AppTests",
                type: "double precision",
                nullable: false,
                defaultValue: 50.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "AppTests");

            migrationBuilder.DropColumn(
                name: "PassScore",
                table: "AppTests");
        }
    }
}
