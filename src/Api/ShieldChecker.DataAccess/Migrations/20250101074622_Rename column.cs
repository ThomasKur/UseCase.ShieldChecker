using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldChecker.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Renamecolumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FirstRunWizard",
                table: "SystemStatus",
                newName: "IsFirstRunCompleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsFirstRunCompleted",
                table: "SystemStatus",
                newName: "FirstRunWizard");
        }
    }
}
