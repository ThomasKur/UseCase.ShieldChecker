using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldChecker.WebApp.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAzureVMSizeWithHyperVSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DcVMSize",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "WorkerVMSize",
                table: "Settings");

            migrationBuilder.AddColumn<int>(
                name: "DcVMCpuCount",
                table: "Settings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "DcVMMemoryMB",
                table: "Settings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "VMStoragePath",
                table: "Settings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WorkerVMCpuCount",
                table: "Settings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "WorkerVMMemoryMB",
                table: "Settings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DcVMCpuCount",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "DcVMMemoryMB",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "VMStoragePath",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "WorkerVMCpuCount",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "WorkerVMMemoryMB",
                table: "Settings");

            migrationBuilder.AddColumn<string>(
                name: "DcVMSize",
                table: "Settings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkerVMSize",
                table: "Settings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
