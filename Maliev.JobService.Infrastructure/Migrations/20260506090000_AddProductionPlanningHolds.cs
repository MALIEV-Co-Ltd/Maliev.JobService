using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.JobService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionPlanningHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_project_id",
                schema: "public",
                table: "jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_project_part_id",
                schema: "public",
                table: "jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "production_planning_holds",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technology = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    machine_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    machine_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    queue_position = table.Column<int>(type: "integer", nullable: false),
                    scheduled_start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scheduled_end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    setup_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    production_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    converted_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_planning_holds", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_source_project_part",
                schema: "public",
                table: "jobs",
                columns: new[] { "source_project_id", "source_project_part_id" });

            migrationBuilder.CreateIndex(
                name: "ix_production_planning_holds_expires_at",
                schema: "public",
                table: "production_planning_holds",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_production_planning_holds_machine_status_start",
                schema: "public",
                table: "production_planning_holds",
                columns: new[] { "machine_id", "status", "scheduled_start_time" });

            migrationBuilder.CreateIndex(
                name: "ix_production_planning_holds_project_part",
                schema: "public",
                table: "production_planning_holds",
                columns: new[] { "project_id", "project_part_id" });

            migrationBuilder.CreateIndex(
                name: "ix_production_planning_holds_technology_status",
                schema: "public",
                table: "production_planning_holds",
                columns: new[] { "technology", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "production_planning_holds",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_jobs_source_project_part",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "source_project_id",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "source_project_part_id",
                schema: "public",
                table: "jobs");
        }
    }
}
