using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldChecker.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Addfixedtesttoscheduler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinition_AutoSchedule_AutoScheduleID",
                table: "TestDefinition");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinition_AutoScheduleID",
                table: "TestDefinition");

            migrationBuilder.DropColumn(
                name: "AutoScheduleID",
                table: "TestDefinition");

            migrationBuilder.CreateTable(
                name: "AutoScheduleTestDefinition",
                columns: table => new
                {
                    AutoSchedulesID = table.Column<int>(type: "int", nullable: false),
                    TestDefinitionsID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoScheduleTestDefinition", x => new { x.AutoSchedulesID, x.TestDefinitionsID });
                    table.ForeignKey(
                        name: "FK_AutoScheduleTestDefinition_AutoSchedule_AutoSchedulesID",
                        column: x => x.AutoSchedulesID,
                        principalTable: "AutoSchedule",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AutoScheduleTestDefinition_TestDefinition_TestDefinitionsID",
                        column: x => x.TestDefinitionsID,
                        principalTable: "TestDefinition",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoScheduleTestDefinition_TestDefinitionsID",
                table: "AutoScheduleTestDefinition",
                column: "TestDefinitionsID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoScheduleTestDefinition");

            migrationBuilder.AddColumn<int>(
                name: "AutoScheduleID",
                table: "TestDefinition",
                type: "int",
                nullable: true);

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
    }
}
