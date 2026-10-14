using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransjapHorimetros.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignAnomalyRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "machine_id",
                table: "anomalies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                EXEC(N'
                UPDATE anomalies
                SET machine_id = hour_meter_readings.machine_id
                FROM anomalies
                INNER JOIN hour_meter_readings
                    ON hour_meter_readings.id = anomalies.reading_id;
                ');
                """);

            migrationBuilder.Sql(
                """
                EXEC(N'
                IF EXISTS (
                    SELECT 1
                    FROM anomalies
                    WHERE machine_id IS NULL
                )
                BEGIN
                    THROW 50001, N''Não foi possível derivar machine_id para todas as anomalias.'', 1;
                END;
                ');
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "machine_id",
                table: "anomalies",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_anomalies_machine_id",
                table: "anomalies",
                column: "machine_id");

            migrationBuilder.AddForeignKey(
                name: "fk_anomalies_machines_machine_id",
                table: "anomalies",
                column: "machine_id",
                principalTable: "machines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AlterColumn<Guid>(
                name: "reading_id",
                table: "anomalies",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_anomalies_machines_machine_id",
                table: "anomalies");

            migrationBuilder.DropIndex(
                name: "ix_anomalies_machine_id",
                table: "anomalies");

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM anomalies
                    WHERE reading_id IS NULL
                )
                BEGIN
                    THROW 50002, 'Não é possível restaurar reading_id como obrigatório enquanto existirem anomalias sem leitura.', 1;
                END;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "reading_id",
                table: "anomalies",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "machine_id",
                table: "anomalies");
        }
    }
}
