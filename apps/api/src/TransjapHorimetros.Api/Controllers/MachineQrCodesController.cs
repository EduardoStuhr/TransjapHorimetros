using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.QrCodes;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class MachineQrCodesController(IMachineQrCodeService qrCodeService) : ControllerBase
{
    /// <summary>Resolve um QR cadastrado ou um conteúdo legado simples (número/FROTA N) para uma máquina.</summary>
    [HttpGet("qr-codes/resolve")]
    [ProducesResponseType(typeof(QrResolutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<QrResolutionResponse> Resolve(
        [FromQuery] string value,
        CancellationToken cancellationToken) =>
        qrCodeService.ResolveAsync(value, cancellationToken);

    /// <summary>Lista o histórico de QR codes de uma máquina, incluindo os desativados.</summary>
    [HttpGet("machines/{machineId:guid}/qr-codes")]
    [ProducesResponseType(typeof(IReadOnlyList<MachineQrCodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<MachineQrCodeResponse>> GetHistory(
        Guid machineId,
        CancellationToken cancellationToken) =>
        qrCodeService.GetHistoryAsync(machineId, cancellationToken);

    /// <summary>Emite ou substitui o QR ativo de uma máquina, preservando o histórico anterior.</summary>
    [HttpPost("machines/{machineId:guid}/qr-codes")]
    [ProducesResponseType(typeof(MachineQrCodeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineQrCodeResponse>> Issue(
        Guid machineId,
        [FromBody] IssueMachineQrCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await qrCodeService.IssueAsync(
            machineId,
            request,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return Created($"/api/v1/machines/{machineId}/qr-codes", result);
    }

    /// <summary>Desativa o QR ativo da máquina sem apagar o histórico.</summary>
    [HttpDelete("machines/{machineId:guid}/qr-codes/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        Guid machineId,
        CancellationToken cancellationToken)
    {
        await qrCodeService.DeactivateActiveAsync(
            machineId,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return NoContent();
    }
}
