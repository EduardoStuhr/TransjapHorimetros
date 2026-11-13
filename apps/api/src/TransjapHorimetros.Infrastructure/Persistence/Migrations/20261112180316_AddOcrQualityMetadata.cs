using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransjapHorimetros.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOcrQualityMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlgorithmVersion",
                table: "photo_ocr_results",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BelowPreviousReading",
                table: "photo_ocr_results",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CandidatesJson",
                table: "photo_ocr_results",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousReadingValue",
                table: "photo_ocr_results",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "photo_ocr_results",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "photo_ocr_results",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // The incremental SQL script can execute all operations as one batch.
            // Compile the filtered index only after SQL Server has added RequestId.
            migrationBuilder.Sql("""
                EXEC sys.sp_executesql N'CREATE UNIQUE INDEX [ux_photo_ocr_request] ON [photo_ocr_results] ([RequestId]) WHERE [RequestId] IS NOT NULL;';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_photo_ocr_request",
                table: "photo_ocr_results");

            migrationBuilder.DropColumn(
                name: "AlgorithmVersion",
                table: "photo_ocr_results");

            migrationBuilder.DropColumn(
                name: "BelowPreviousReading",
                table: "photo_ocr_results");

            migrationBuilder.DropColumn(
                name: "CandidatesJson",
                table: "photo_ocr_results");

            migrationBuilder.DropColumn(
                name: "PreviousReadingValue",
                table: "photo_ocr_results");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "photo_ocr_results");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "photo_ocr_results");
        }
    }
}
