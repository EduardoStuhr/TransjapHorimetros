using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransjapHorimetros.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    entity = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    entity_id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    old_value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    new_value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    correlation_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "machines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fleet_number = table.Column<int>(type: "int", nullable: false),
                    model = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machines", x => x.id);
                    table.CheckConstraint("ck_machines_fleet_number_positive", "fleet_number > 0");
                });

            migrationBuilder.CreateTable(
                name: "work_sites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_sites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hour_meter_readings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    machine_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    work_site_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    value = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    reading_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    captured_at_device = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    received_at_server = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    synced_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    client_event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hour_meter_readings", x => x.id);
                    table.CheckConstraint("ck_hour_meter_readings_value_non_negative", "value >= 0");
                    table.ForeignKey(
                        name: "fk_hour_meter_readings_machines_machine_id",
                        column: x => x.machine_id,
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_hour_meter_readings_work_sites_work_site_id",
                        column: x => x.work_site_id,
                        principalTable: "work_sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "anomalies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reading_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anomalies", x => x.id);
                    table.ForeignKey(
                        name: "fk_anomalies_hour_meter_readings_reading_id",
                        column: x => x.reading_id,
                        principalTable: "hour_meter_readings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_anomalies_reading_id",
                table: "anomalies",
                column: "reading_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_entity_id",
                table: "audit_logs",
                columns: new[] { "entity", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_hour_meter_readings_machine_captured_at",
                table: "hour_meter_readings",
                columns: new[] { "machine_id", "captured_at_device" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_hour_meter_readings_status",
                table: "hour_meter_readings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_hour_meter_readings_work_site_id",
                table: "hour_meter_readings",
                column: "work_site_id");

            migrationBuilder.CreateIndex(
                name: "ux_hour_meter_readings_client_event_id",
                table: "hour_meter_readings",
                column: "client_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_machines_fleet_number",
                table: "machines",
                column: "fleet_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_sites_code",
                table: "work_sites",
                column: "code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anomalies");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "hour_meter_readings");

            migrationBuilder.DropTable(
                name: "machines");

            migrationBuilder.DropTable(
                name: "work_sites");
        }
    }
}
