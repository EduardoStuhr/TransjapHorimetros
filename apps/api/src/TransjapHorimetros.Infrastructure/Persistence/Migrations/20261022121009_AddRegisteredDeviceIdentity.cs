using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransjapHorimetros.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegisteredDeviceIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "registered_device_id",
                table: "hour_meter_readings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "registered_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    local_device_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    platform = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    public_key = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    public_key_fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    key_algorithm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    requested_by_email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    requested_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    authorized_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    authorized_by_email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    authorized_at = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    revoked_by_email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    revoked_at = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    last_seen_at = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registered_devices", x => x.id);
                    table.CheckConstraint("ck_registered_devices_status", "status IN ('PENDING', 'AUTHORIZED', 'REVOKED')");
                    table.ForeignKey(
                        name: "FK_registered_devices_users_authorized_by_user_id",
                        column: x => x.authorized_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registered_devices_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registered_devices_users_revoked_by_user_id",
                        column: x => x.revoked_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    registered_device_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    nonce = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    canonical_payload = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    issued_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_challenges", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_challenges_registered_devices_registered_device_id",
                        column: x => x.registered_device_id,
                        principalTable: "registered_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hour_meter_readings_registered_device_id",
                table: "hour_meter_readings",
                column: "registered_device_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_challenges_device_expiry",
                table: "device_challenges",
                columns: new[] { "registered_device_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_device_challenges_nonce",
                table: "device_challenges",
                column: "nonce",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registered_devices_authorized_by_user_id",
                table: "registered_devices",
                column: "authorized_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_registered_devices_requested_by_user_id",
                table: "registered_devices",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_registered_devices_revoked_by_user_id",
                table: "registered_devices",
                column: "revoked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_registered_devices_status_requested_at",
                table: "registered_devices",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ux_registered_devices_key_fingerprint",
                table: "registered_devices",
                column: "public_key_fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_registered_devices_local_device_id",
                table: "registered_devices",
                column: "local_device_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_hour_meter_readings_registered_devices_registered_device_id",
                table: "hour_meter_readings",
                column: "registered_device_id",
                principalTable: "registered_devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_hour_meter_readings_registered_devices_registered_device_id",
                table: "hour_meter_readings");

            migrationBuilder.DropTable(
                name: "device_challenges");

            migrationBuilder.DropTable(
                name: "registered_devices");

            migrationBuilder.DropIndex(
                name: "IX_hour_meter_readings_registered_device_id",
                table: "hour_meter_readings");

            migrationBuilder.DropColumn(
                name: "registered_device_id",
                table: "hour_meter_readings");
        }
    }
}
