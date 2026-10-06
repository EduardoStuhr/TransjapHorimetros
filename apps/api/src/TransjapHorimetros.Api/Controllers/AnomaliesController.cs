using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Anomalies;
using TransjapHorimetros.Application.Common;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/anomalies")]
public sealed class AnomaliesController(IAnomalyService anomalyService) : ControllerBase
{
    /// <summary>Lista anomalias reais detectadas nas leituras.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AnomalyResponse>), StatusCodes.Status200OK)]
    public Task<PagedResponse<AnomalyResponse>> GetAll(
        [FromQuery] AnomalyQuery query,
        CancellationToken cancellationToken) =>
        anomalyService.GetPageAsync(query, cancellationToken);

    /// <summary>Obtém uma anomalia por identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AnomalyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<AnomalyResponse> GetById(Guid id, CancellationToken cancellationToken) =>
        anomalyService.GetByIdAsync(id, cancellationToken);
}
