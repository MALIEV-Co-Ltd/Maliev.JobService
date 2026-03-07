using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.JobService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Technology = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VolumeCm3 = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    EstimatedPrintTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    AssignedMachineId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AssignedMachineId",
                table: "Jobs",
                column: "AssignedMachineId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_OrderId",
                table: "Jobs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_OrderId_OrderItemId",
                table: "Jobs",
                columns: new[] { "OrderId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Priority",
                table: "Jobs",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status",
                table: "Jobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}
