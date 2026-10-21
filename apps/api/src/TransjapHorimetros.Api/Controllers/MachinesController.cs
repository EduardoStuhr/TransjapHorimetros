using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Machines;
using TransjapHorimetros.Application.QrCodes;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Application.Security;
using TransjapHorimetros.Domain.Security;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/machines")]
public sealed class MachinesController(
    IMachineService machineService,
    IReadingService readingService,
    IMachineQrCodeService machineQrCodeService) : ControllerBase
{
    /// <summary>Lista máquinas com paginação e filtros.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<MachineListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<PagedResponse<MachineListItemResponse>> GetAll(
        [FromQuery] MachineQuery query,
        CancellationToken cancellationToken) =>
        machineService.GetPageAsync(query, cancellationToken);

    /// <summary>Obtém os dados e o histórico recente de uma máquina.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MachineDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<MachineDetailsResponse> GetById(Guid id, CancellationToken cancellationToken) =>
        machineService.GetByIdAsync(id, cancellationToken);

    /// <summary>Obtém uma máquina pelo número de frota.</summary>
    [HttpGet("by-fleet/{fleetNumber:int}")]
    [ProducesResponseType(typeof(MachineDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<MachineDetailsResponse> GetByFleetNumber(
        int fleetNumber,
        CancellationToken cancellationToken) =>
        machineService.GetByFleetNumberAsync(fleetNumber, cancellationToken);

    /// <summary>Cadastra uma máquina e registra auditoria.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.CanManageMachines)]
    [ProducesResponseType(typeof(MachineDetailsResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineDetailsResponse>> Create(
        [FromBody] CreateMachineRequest request,
        CancellationToken cancellationToken)
    {
        var machine = await machineService.CreateAsync(
            request,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = machine.Id }, machine);
    }

    /// <summary>Atualiza cadastro e estado ativo/inativo sem apagar histórico.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageMachines)]
    [ProducesResponseType(typeof(MachineDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<MachineDetailsResponse> Update(
        Guid id,
        [FromBody] UpdateMachineRequest request,
        CancellationToken cancellationToken) =>
        machineService.UpdateAsync(id, request, HttpContext.TraceIdentifier, cancellationToken);

    /// <summary>Configura a unidade oficial da máquina sem alterar os demais dados cadastrais.</summary>
    [HttpPatch("{id:guid}/meter-unit")]
    [Authorize(Roles = AppRoles.Manager + "," + AppRoles.Admin)]
    [ProducesResponseType(typeof(MachineDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<MachineDetailsResponse> ConfigureMeterUnit(
        Guid id,
        [FromBody] ConfigureMachineMeterUnitRequest request,
        CancellationToken cancellationToken) =>
        machineService.ConfigureMeterUnitAsync(
            id,
            request,
            HttpContext.TraceIdentifier,
            cancellationToken);

    /// <summary>Lista leituras de uma máquina, mais recentes primeiro.</summary>
    [HttpGet("{machineId:guid}/readings")]
    [ProducesResponseType(typeof(PagedResponse<ReadingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<PagedResponse<ReadingResponse>> GetReadings(
        Guid machineId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        readingService.GetForMachineAsync(machineId, page, pageSize, cancellationToken);

    /// <summary>Lista o histórico completo de QR codes da máquina, mais recentes primeiro.</summary>
    [HttpGet("{machineId:guid}/qr-codes")]
    [Authorize(Policy = Policies.CanViewQrHistory)]
    [ProducesResponseType(typeof(IReadOnlyList<MachineQrCodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<MachineQrCodeResponse>> GetQrCodes(
        Guid machineId,
        CancellationToken cancellationToken) =>
        machineQrCodeService.GetHistoryAsync(machineId, cancellationToken);

    /// <summary>Emite ou substitui o QR code ativo, preservando o histórico.</summary>
    [HttpPost("{machineId:guid}/qr-codes")]
    [Authorize(Policy = Policies.CanManageQrCodes)]
    [ProducesResponseType(typeof(MachineQrCodeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineQrCodeResponse>> IssueQrCode(
        Guid machineId,
        [FromBody] IssueMachineQrCodeRequest request,
        CancellationToken cancellationToken)
    {
        var qrCode = await machineQrCodeService.IssueAsync(
            machineId,
            request,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return Created($"/api/v1/machines/{machineId}/qr-codes", qrCode);
    }

    /// <summary>Desativa logicamente o QR code ativo da máquina.</summary>
    [HttpDelete("{machineId:guid}/qr-codes/active")]
    [Authorize(Policy = Policies.CanManageQrCodes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateActiveQrCode(
        Guid machineId,
        CancellationToken cancellationToken)
    {
        await machineQrCodeService.DeactivateActiveAsync(
            machineId,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return NoContent();
    }
}
