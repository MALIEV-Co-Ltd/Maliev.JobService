using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.JobService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobStatusTransitionAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_status_transition_audits",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    new_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_status_transition_audits", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_status_transition_audits_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "public",
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_status_transition_audits_changed_at_utc",
                schema: "public",
                table: "job_status_transition_audits",
                column: "changed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_job_status_transition_audits_job_id",
                schema: "public",
                table: "job_status_transition_audits",
                column: "job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_status_transition_audits",
                schema: "public");
        }
    }
}
