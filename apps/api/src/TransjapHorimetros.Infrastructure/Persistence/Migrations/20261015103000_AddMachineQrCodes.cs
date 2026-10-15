using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TransjapHorimetros.Infrastructure.Persistence;

#nullable disable

namespace TransjapHorimetros.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TransjapDbContext))]
[Migration("20261015103000_AddMachineQrCodes")]
public partial class AddMachineQrCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "machine_qr_codes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                machine_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                token = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                is_active = table.Column<bool>(type: "bit", nullable: false),
                deactivated_at = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                created_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                updated_at = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_machine_qr_codes", x => x.id);
                table.ForeignKey(
                    name: "fk_machine_qr_codes_machines_machine_id",
                    column: x => x.machine_id,
                    principalTable: "machines",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_machine_qr_codes_machine_active",
            table: "machine_qr_codes",
            column: "machine_id",
            unique: true,
            filter: "[is_active] = 1");

        migrationBuilder.CreateIndex(
            name: "ux_machine_qr_codes_token",
            table: "machine_qr_codes",
            column: "token",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "machine_qr_codes");
    }
}
