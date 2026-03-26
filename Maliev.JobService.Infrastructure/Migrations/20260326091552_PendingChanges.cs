using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.JobService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Jobs",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_OrderId_OrderItemId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_Priority",
                table: "Jobs");

            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.RenameTable(
                name: "Jobs",
                newName: "jobs",
                newSchema: "public");

            migrationBuilder.RenameColumn(
                name: "Technology",
                schema: "public",
                table: "jobs",
                newName: "technology");

            migrationBuilder.RenameColumn(
                name: "Status",
                schema: "public",
                table: "jobs",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Priority",
                schema: "public",
                table: "jobs",
                newName: "priority");

            migrationBuilder.RenameColumn(
                name: "Notes",
                schema: "public",
                table: "jobs",
                newName: "notes");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "public",
                table: "jobs",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "VolumeCm3",
                schema: "public",
                table: "jobs",
                newName: "volume_cm3");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                schema: "public",
                table: "jobs",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "StartedAt",
                schema: "public",
                table: "jobs",
                newName: "started_at");

            migrationBuilder.RenameColumn(
                name: "OrderItemId",
                schema: "public",
                table: "jobs",
                newName: "order_item_id");

            migrationBuilder.RenameColumn(
                name: "OrderId",
                schema: "public",
                table: "jobs",
                newName: "order_id");

            migrationBuilder.RenameColumn(
                name: "MaterialId",
                schema: "public",
                table: "jobs",
                newName: "material_id");

            migrationBuilder.RenameColumn(
                name: "EstimatedPrintTimeMinutes",
                schema: "public",
                table: "jobs",
                newName: "estimated_print_time_minutes");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "public",
                table: "jobs",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                schema: "public",
                table: "jobs",
                newName: "completed_at");

            migrationBuilder.RenameColumn(
                name: "AssignedMachineId",
                schema: "public",
                table: "jobs",
                newName: "assigned_machine_id");

            migrationBuilder.RenameIndex(
                name: "IX_Jobs_Status",
                schema: "public",
                table: "jobs",
                newName: "ix_jobs_status");

            migrationBuilder.RenameIndex(
                name: "IX_Jobs_OrderId",
                schema: "public",
                table: "jobs",
                newName: "ix_jobs_order_id");

            migrationBuilder.RenameIndex(
                name: "IX_Jobs_AssignedMachineId",
                schema: "public",
                table: "jobs",
                newName: "ix_jobs_assigned_machine_id");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "public",
                table: "jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "priority",
                schema: "public",
                table: "jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "volume_cm3",
                schema: "public",
                table: "jobs",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,3)",
                oldPrecision: 10,
                oldScale: 3);

            migrationBuilder.AddColumn<int>(
                name: "queue_position",
                schema: "public",
                table: "jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "scheduled_end_time",
                schema: "public",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "scheduled_start_time",
                schema: "public",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "setup_time_minutes",
                schema: "public",
                table: "jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_jobs",
                schema: "public",
                table: "jobs",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_jobs",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "queue_position",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "scheduled_end_time",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "scheduled_start_time",
                schema: "public",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "setup_time_minutes",
                schema: "public",
                table: "jobs");

            migrationBuilder.RenameTable(
                name: "jobs",
                schema: "public",
                newName: "Jobs");

            migrationBuilder.RenameColumn(
                name: "technology",
                table: "Jobs",
                newName: "Technology");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "Jobs",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "priority",
                table: "Jobs",
                newName: "Priority");

            migrationBuilder.RenameColumn(
                name: "notes",
                table: "Jobs",
                newName: "Notes");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Jobs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "volume_cm3",
                table: "Jobs",
                newName: "VolumeCm3");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Jobs",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "started_at",
                table: "Jobs",
                newName: "StartedAt");

            migrationBuilder.RenameColumn(
                name: "order_item_id",
                table: "Jobs",
                newName: "OrderItemId");

            migrationBuilder.RenameColumn(
                name: "order_id",
                table: "Jobs",
                newName: "OrderId");

            migrationBuilder.RenameColumn(
                name: "material_id",
                table: "Jobs",
                newName: "MaterialId");

            migrationBuilder.RenameColumn(
                name: "estimated_print_time_minutes",
                table: "Jobs",
                newName: "EstimatedPrintTimeMinutes");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Jobs",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "completed_at",
                table: "Jobs",
                newName: "CompletedAt");

            migrationBuilder.RenameColumn(
                name: "assigned_machine_id",
                table: "Jobs",
                newName: "AssignedMachineId");

            migrationBuilder.RenameIndex(
                name: "ix_jobs_status",
                table: "Jobs",
                newName: "IX_Jobs_Status");

            migrationBuilder.RenameIndex(
                name: "ix_jobs_order_id",
                table: "Jobs",
                newName: "IX_Jobs_OrderId");

            migrationBuilder.RenameIndex(
                name: "ix_jobs_assigned_machine_id",
                table: "Jobs",
                newName: "IX_Jobs_AssignedMachineId");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Jobs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                table: "Jobs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "VolumeCm3",
                table: "Jobs",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Jobs",
                table: "Jobs",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_OrderId_OrderItemId",
                table: "Jobs",
                columns: new[] { "OrderId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Priority",
                table: "Jobs",
                column: "Priority");
        }
    }
}
