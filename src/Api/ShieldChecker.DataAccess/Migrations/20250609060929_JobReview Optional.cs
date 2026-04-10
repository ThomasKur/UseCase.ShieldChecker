using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShieldChecker.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class JobReviewOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "JobReview",
                table: "Settings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobReview",
                table: "Settings");
        }
    }
}
