using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldChecker.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoScheduleTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoScheduleID",
                table: "TestDefinition",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutoSchedule",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    NextExecution = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    FilterRandomCount = table.Column<int>(type: "int", nullable: true),
                    FilterOperatingSystem = table.Column<int>(type: "int", nullable: true),
                    FilterExecution = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoSchedule", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinition_AutoScheduleID",
                table: "TestDefinition",
                column: "AutoScheduleID");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinition_AutoSchedule_AutoScheduleID",
                table: "TestDefinition",
                column: "AutoScheduleID",
                principalTable: "AutoSchedule",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinition_AutoSchedule_AutoScheduleID",
                table: "TestDefinition");

            migrationBuilder.DropTable(
                name: "AutoSchedule");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinition_AutoScheduleID",
                table: "TestDefinition");

            migrationBuilder.DropColumn(
                name: "AutoScheduleID",
                table: "TestDefinition");
        }
    }
}
