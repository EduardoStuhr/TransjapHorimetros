using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransjapHorimetros.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "photo_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reading_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    storage_provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    storage_container = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: false),
                    storage_object_key = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    original_file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    captured_at_device = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    uploaded_at_server = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    width = table.Column<int>(type: "int", nullable: true),
                    height = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_by_email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    client_event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_evidence", x => x.id);
                    table.CheckConstraint("ck_photo_evidence_file_size_positive", "file_size_bytes > 0");
                    table.ForeignKey(
                        name: "fk_photo_evidence_hour_meter_readings_reading_id",
                        column: x => x.reading_id,
                        principalTable: "hour_meter_readings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_photo_evidence_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photo_evidence_created_by_user_id",
                table: "photo_evidence",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_photo_evidence_sha256",
                table: "photo_evidence",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "ux_photo_evidence_client_event_id",
                table: "photo_evidence",
                column: "client_event_id",
                unique: true,
                filter: "[client_event_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_photo_evidence_reading_id",
                table: "photo_evidence",
                column: "reading_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_photo_evidence_storage_object_key",
                table: "photo_evidence",
                column: "storage_object_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photo_evidence");
        }
    }
}
