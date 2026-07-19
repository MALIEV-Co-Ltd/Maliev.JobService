using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.JobService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobProductionSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "configuration_snapshot_json",
                schema: "public",
                table: "jobs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "material_snapshot_json",
                schema: "public",
                table: "jobs",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "configuration_snapshot_json",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "material_snapshot_json",
                schema: "public",
                table: "jobs");
        }
    }
}
