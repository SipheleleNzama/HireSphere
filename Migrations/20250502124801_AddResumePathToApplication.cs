using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireSphere.Migrations
{
    /// <inheritdoc />
    public partial class AddResumePathToApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResumePath",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResumePath",
                table: "Applications");
        }
    }
}
