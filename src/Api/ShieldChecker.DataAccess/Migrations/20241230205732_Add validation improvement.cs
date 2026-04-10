using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldChecker.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Addvalidationimprovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DomainFQDN",
                table: "Settings",
                type: "nvarchar(253)",
                maxLength: 253,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DomainFQDN",
                table: "Settings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(253)",
                oldMaxLength: 253);
        }
    }
}
