using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.WorkSites;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/worksites")]
public sealed class WorkSitesController(IWorkSiteService workSiteService) : ControllerBase
{
    /// <summary>Lista obras reais cadastradas; o banco inicia sem obras.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WorkSiteResponse>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<WorkSiteResponse>> GetAll(
        [FromQuery] bool? active,
        CancellationToken cancellationToken) =>
        workSiteService.GetAllAsync(active, cancellationToken);

    /// <summary>Obtém uma obra por identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkSiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<WorkSiteResponse> GetById(Guid id, CancellationToken cancellationToken) =>
        workSiteService.GetByIdAsync(id, cancellationToken);

    /// <summary>Cadastra uma obra e registra auditoria.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(WorkSiteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkSiteResponse>> Create(
        [FromBody] CreateWorkSiteRequest request,
        CancellationToken cancellationToken)
    {
        var workSite = await workSiteService.CreateAsync(
            request,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = workSite.Id }, workSite);
    }

    /// <summary>Atualiza uma obra sem excluir seu histórico.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WorkSiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<WorkSiteResponse> Update(
        Guid id,
        [FromBody] UpdateWorkSiteRequest request,
        CancellationToken cancellationToken) =>
        workSiteService.UpdateAsync(id, request, HttpContext.TraceIdentifier, cancellationToken);
}
