using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransjapHorimetros.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteReadingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "correction_of_reading_id",
                table: "hour_meter_readings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_email",
                table: "hour_meter_readings",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "hour_meter_readings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "device_id",
                table: "hour_meter_readings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "difference_from_previous",
                table: "hour_meter_readings",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "meter_unit",
                table: "hour_meter_readings",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "previous_reading_id",
                table: "hour_meter_readings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_hour_meter_readings_correction_of_reading_id",
                table: "hour_meter_readings",
                column: "correction_of_reading_id");

            migrationBuilder.CreateIndex(
                name: "IX_hour_meter_readings_created_by_user_id",
                table: "hour_meter_readings",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_hour_meter_readings_previous_reading_id",
                table: "hour_meter_readings",
                column: "previous_reading_id");

            migrationBuilder.CreateIndex(
                name: "ix_readings_capture_order",
                table: "hour_meter_readings",
                columns: new[] { "captured_at_device", "received_at_server", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_readings_operational_order",
                table: "hour_meter_readings",
                columns: new[] { "machine_id", "meter_unit", "captured_at_device", "received_at_server", "id" },
                descending: new[] { false, false, true, true, true });

            migrationBuilder.AddCheckConstraint(
                name: "ck_readings_correction_link",
                table: "hour_meter_readings",
                sql: "correction_of_reading_id IS NULL OR (reading_type = 'CORRECTION' AND correction_of_reading_id <> id)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_readings_meter_unit",
                table: "hour_meter_readings",
                sql: "meter_unit IS NULL OR meter_unit IN ('HOURS', 'KM')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_readings_previous_pair",
                table: "hour_meter_readings",
                sql: "(previous_reading_id IS NULL AND difference_from_previous IS NULL) OR (previous_reading_id IS NOT NULL AND difference_from_previous IS NOT NULL AND previous_reading_id <> id)");

            migrationBuilder.AddForeignKey(
                name: "FK_hour_meter_readings_hour_meter_readings_correction_of_reading_id",
                table: "hour_meter_readings",
                column: "correction_of_reading_id",
                principalTable: "hour_meter_readings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hour_meter_readings_hour_meter_readings_previous_reading_id",
                table: "hour_meter_readings",
                column: "previous_reading_id",
                principalTable: "hour_meter_readings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hour_meter_readings_users_created_by_user_id",
                table: "hour_meter_readings",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_hour_meter_readings_hour_meter_readings_correction_of_reading_id",
                table: "hour_meter_readings");

            migrationBuilder.DropForeignKey(
                name: "FK_hour_meter_readings_hour_meter_readings_previous_reading_id",
                table: "hour_meter_readings");

            migrationBuilder.DropForeignKey(
                name: "FK_hour_meter_readings_users_created_by_user_id",
                table: "hour_meter_readings");

            migrationBuilder.DropIndex(
                name: "IX_hour_meter_readings_correction_of_reading_id",
                table: "hour_meter_readings");

            migrationBuilder.DropIndex(
                name: "IX_hour_meter_readings_created_by_user_id",
                table: "hour_meter_readings");

            migrationBuilder.DropIndex(
                name: "IX_hour_meter_readings_previous_reading_id",
                table: "hour_meter_readings");

            migrationBuilder.DropIndex(
                name: "ix_readings_capture_order",
                table: "hour_meter_readings");

            migrationBuilder.DropIndex(
                name: "ix_readings_operational_order",
                table: "hour_meter_readings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_readings_correction_link",
                table: "hour_meter_readings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_readings_meter_unit",
                table: "hour_meter_readings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_readings_previous_pair",
                table: "hour_meter_readings");

            migrationBuilder.DropColumn(
                name: "correction_of_reading_id",
                table: "hour_meter_readings");

            migrationBuilder.DropColumn(
                name: "created_by_email",
                table: "hour_meter_readings");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "hour_meter_readings");

            migrationBuilder.DropColumn(
                name: "device_id",
                table: "hour_meter_readings");

            migrationBuilder.DropColumn(
                name: "difference_from_previous",
                table: "hour_meter_readings");

            migrationBuilder.DropColumn(
                name: "meter_unit",
                table: "hour_meter_readings");

            migrationBuilder.DropColumn(
                name: "previous_reading_id",
                table: "hour_meter_readings");
        }
    }
}
