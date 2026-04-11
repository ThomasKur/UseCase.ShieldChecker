using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldChecker.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulerMutex",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Owner = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulerMutex", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false),
                    MaxWorkerCount = table.Column<int>(type: "int", nullable: false),
                    JobTimeout = table.Column<int>(type: "int", nullable: false),
                    WorkerVMSize = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkerVMWindowsImage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkerVMLinuxImage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DcVMSize = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DcVMImage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DomainFQDN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DomainControllerName = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    MDEWindowsOnboardingScript = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MDELinuxOnboardingScript = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MDIKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.ID);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SettingsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "SystemStatus",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false),
                    FirstRunWizard = table.Column<bool>(type: "bit", nullable: false),
                    DomainControllerStatus = table.Column<int>(type: "int", nullable: false),
                    WebAppVersion = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemStatus", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "UserInfo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserPrincipalName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInfo", x => x.Id);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserInfoHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "TestDefinition",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MitreTechnique = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedAlertTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "1"),
                    ReadOnly = table.Column<bool>(type: "bit", nullable: true),
                    ScriptTest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScriptPrerequisites = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScriptCleanup = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ElevationRequired = table.Column<bool>(type: "bit", nullable: false),
                    OperatingSystem = table.Column<int>(type: "int", nullable: false),
                    ExecutorSystemType = table.Column<int>(type: "int", nullable: false),
                    ExecutorUserType = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDefinition", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TestDefinition_UserInfo_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "UserInfo",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TestDefinition_UserInfo_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "UserInfo",
                        principalColumn: "Id");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TestDefinitionHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "TestJob",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UseCaseID = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkerStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkerEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    WorkerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkerIP = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkerRemoteIP = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TestUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TestOutput = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SchedulerLog = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetectedAlerts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewResult = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestJob", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TestJob_TestDefinition_UseCaseID",
                        column: x => x.UseCaseID,
                        principalTable: "TestDefinition",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinition_CreatedById",
                table: "TestDefinition",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinition_ModifiedById",
                table: "TestDefinition",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestJob_UseCaseID",
                table: "TestJob",
                column: "UseCaseID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulerMutex");

            migrationBuilder.DropTable(
                name: "Settings")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SettingsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "SystemStatus");

            migrationBuilder.DropTable(
                name: "TestJob");

            migrationBuilder.DropTable(
                name: "TestDefinition")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TestDefinitionHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "UserInfo")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserInfoHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
