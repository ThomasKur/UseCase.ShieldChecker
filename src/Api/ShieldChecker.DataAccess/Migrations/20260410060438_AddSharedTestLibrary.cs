using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldChecker.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedTestLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SharedLibrarySourceId",
                table: "TestDefinition",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SharedTestDefinition",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MitreTechnique = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedAlertTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ScriptTest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScriptPrerequisites = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScriptCleanup = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ElevationRequired = table.Column<bool>(type: "bit", nullable: false),
                    OperatingSystem = table.Column<int>(type: "int", nullable: false),
                    ExecutorSystemType = table.Column<int>(type: "int", nullable: false),
                    ExecutorUserType = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedTestDefinition", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SharedTestDefinition_UserInfo_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "UserInfo",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SharedTestDefinition_UserInfo_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "UserInfo",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SharedTestConsumption",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SharedTestDefinitionId = table.Column<int>(type: "int", nullable: false),
                    ConsumedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedTestConsumption", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SharedTestConsumption_SharedTestDefinition_SharedTestDefinitionId",
                        column: x => x.SharedTestDefinitionId,
                        principalTable: "SharedTestDefinition",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedTestConsumption_UserInfo_ConsumedByUserId",
                        column: x => x.ConsumedByUserId,
                        principalTable: "UserInfo",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinition_SharedLibrarySourceId",
                table: "TestDefinition",
                column: "SharedLibrarySourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedTestConsumption_ConsumedByUserId",
                table: "SharedTestConsumption",
                column: "ConsumedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedTestConsumption_SharedTestDefinitionId_ConsumedByUserId",
                table: "SharedTestConsumption",
                columns: new[] { "SharedTestDefinitionId", "ConsumedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedTestDefinition_ApprovedById",
                table: "SharedTestDefinition",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_SharedTestDefinition_SubmittedById",
                table: "SharedTestDefinition",
                column: "SubmittedById");

            migrationBuilder.AddForeignKey(
                name: "FK_TestDefinition_SharedTestDefinition_SharedLibrarySourceId",
                table: "TestDefinition",
                column: "SharedLibrarySourceId",
                principalTable: "SharedTestDefinition",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestDefinition_SharedTestDefinition_SharedLibrarySourceId",
                table: "TestDefinition");

            migrationBuilder.DropTable(
                name: "SharedTestConsumption");

            migrationBuilder.DropTable(
                name: "SharedTestDefinition");

            migrationBuilder.DropIndex(
                name: "IX_TestDefinition_SharedLibrarySourceId",
                table: "TestDefinition");

            migrationBuilder.DropColumn(
                name: "SharedLibrarySourceId",
                table: "TestDefinition");
        }
    }
}
