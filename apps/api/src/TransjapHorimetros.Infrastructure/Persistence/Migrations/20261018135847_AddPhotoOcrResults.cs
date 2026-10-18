using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransjapHorimetros.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoOcrResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ocr_result_id",
                table: "anomalies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "photo_ocr_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhotoEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProviderModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RawText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DetectedValue = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(9,8)", precision: 9, scale: 8, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProcessedAtServer = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ProcessingDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    AbsoluteDifference = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    PercentageDifference = table.Column<decimal>(type: "decimal(24,8)", precision: 24, scale: 8, nullable: true),
                    MatchesOperatorValue = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_ocr_results", x => x.Id);
                    table.CheckConstraint("ck_photo_ocr_confidence", "[Confidence] IS NULL OR ([Confidence] >= 0 AND [Confidence] <= 1)");
                    table.ForeignKey(
                        name: "FK_photo_ocr_results_photo_evidence_PhotoEvidenceId",
                        column: x => x.PhotoEvidenceId,
                        principalTable: "photo_evidence",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_anomalies_ocr_result_type",
                table: "anomalies",
                columns: new[] { "ocr_result_id", "type" },
                unique: true,
                filter: "[ocr_result_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_photo_ocr_results_PhotoEvidenceId_CreatedAt",
                table: "photo_ocr_results",
                columns: new[] { "PhotoEvidenceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_photo_ocr_results_ProcessedAtServer",
                table: "photo_ocr_results",
                column: "ProcessedAtServer");

            migrationBuilder.CreateIndex(
                name: "IX_photo_ocr_results_Status",
                table: "photo_ocr_results",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_photo_ocr_initial",
                table: "photo_ocr_results",
                column: "PhotoEvidenceId",
                unique: true,
                filter: "[IsInitial] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_anomalies_photo_ocr_results_ocr_result_id",
                table: "anomalies",
                column: "ocr_result_id",
                principalTable: "photo_ocr_results",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_anomalies_photo_ocr_results_ocr_result_id",
                table: "anomalies");

            migrationBuilder.DropTable(
                name: "photo_ocr_results");

            migrationBuilder.DropIndex(
                name: "ux_anomalies_ocr_result_type",
                table: "anomalies");

            migrationBuilder.DropColumn(
                name: "ocr_result_id",
                table: "anomalies");
        }
    }
}
