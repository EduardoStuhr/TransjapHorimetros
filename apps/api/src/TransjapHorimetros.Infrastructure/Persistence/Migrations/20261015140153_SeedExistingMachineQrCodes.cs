using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransjapHorimetros.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedExistingMachineQrCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ExistingMachineQrCodeDataPatch.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vazio: QR físico é histórico operacional e não pode ser apagado.
        }
    }
}
