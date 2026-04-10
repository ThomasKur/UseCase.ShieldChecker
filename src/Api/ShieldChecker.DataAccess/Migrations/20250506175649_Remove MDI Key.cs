using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldChecker.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMDIKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MDIKey",
                table: "Settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MDIKey",
                table: "Settings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
