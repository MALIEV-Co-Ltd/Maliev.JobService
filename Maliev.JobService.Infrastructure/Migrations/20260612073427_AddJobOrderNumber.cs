using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.JobService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOrderNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "order_number",
                schema: "public",
                table: "jobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "order_number",
                schema: "public",
                table: "jobs");
        }
    }
}
