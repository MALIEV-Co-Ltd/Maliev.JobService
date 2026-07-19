using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.JobService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_jobs_machine_queue_position",
                schema: "public",
                table: "jobs",
                columns: new[] { "assigned_machine_id", "queue_position" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_machine_scheduled_start",
                schema: "public",
                table: "jobs",
                columns: new[] { "assigned_machine_id", "scheduled_start_time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_jobs_machine_queue_position",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "ix_jobs_machine_scheduled_start",
                schema: "public",
                table: "jobs");
        }
    }
}
