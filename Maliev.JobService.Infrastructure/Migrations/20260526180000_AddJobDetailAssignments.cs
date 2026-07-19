using Maliev.JobService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.JobService.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(JobDbContext))]
    [Migration("20260526180000_AddJobDetailAssignments")]
    public partial class AddJobDetailAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assigned_operator",
                schema: "public",
                table: "jobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_id",
                schema: "public",
                table: "jobs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_name",
                schema: "public",
                table: "jobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_jobs_customer_id",
                schema: "public",
                table: "jobs",
                column: "customer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_jobs_customer_id",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "assigned_operator",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "customer_id",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "customer_name",
                schema: "public",
                table: "jobs");
        }
    }
}
