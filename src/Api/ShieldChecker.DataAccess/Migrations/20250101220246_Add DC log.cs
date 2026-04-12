using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldChecker.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddDClog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ScheduleraddType",
                table: "SchedulerMutex",
                newName: "SchedulerType");

            migrationBuilder.AddColumn<string>(
                name: "DomainControllerLog",
                table: "SystemStatus",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DomainControllerLog",
                table: "SystemStatus");

            migrationBuilder.RenameColumn(
                name: "SchedulerType",
                table: "SchedulerMutex",
                newName: "ScheduleraddType");
        }
    }
}
