using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.QrCodes;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/qr-codes")]
public sealed class QrCodesController(IMachineQrCodeService machineQrCodeService) : ControllerBase
{
    /// <summary>Resolve um token formal ativo ou um identificador legado conservador de frota.</summary>
    [HttpGet("resolve")]
    [ProducesResponseType(typeof(QrResolutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<QrResolutionResponse> Resolve(
        [FromQuery] string value,
        CancellationToken cancellationToken) =>
        machineQrCodeService.ResolveAsync(value, cancellationToken);
}
