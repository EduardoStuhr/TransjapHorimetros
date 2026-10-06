using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Dashboard;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>Retorna indicadores calculados exclusivamente a partir do banco.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryResponse), StatusCodes.Status200OK)]
    public Task<DashboardSummaryResponse> GetSummary(CancellationToken cancellationToken) =>
        dashboardService.GetSummaryAsync(cancellationToken);
}
