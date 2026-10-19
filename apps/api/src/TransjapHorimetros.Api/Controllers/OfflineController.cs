using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Offline;
using TransjapHorimetros.Application.Security;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/offline")]
public sealed class OfflineController(IOfflineReferenceDataService referenceDataService) : ControllerBase
{
    /// <summary>Retorna somente dados ativos necessários para captura confiável sem conexão.</summary>
    [HttpGet("reference-data")]
    [Authorize(Policy = Policies.CanResolveQr)]
    [ProducesResponseType(typeof(OfflineReferenceDataResponse), StatusCodes.Status200OK)]
    public Task<OfflineReferenceDataResponse> GetReferenceData(CancellationToken cancellationToken) =>
        referenceDataService.GetAsync(cancellationToken);
}
