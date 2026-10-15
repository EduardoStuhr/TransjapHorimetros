using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Dashboard;
using TransjapHorimetros.Application.Security;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Policy = Policies.CanViewDashboard)]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>Retorna indicadores calculados exclusivamente a partir do banco.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryResponse), StatusCodes.Status200OK)]
    public Task<DashboardSummaryResponse> GetSummary(CancellationToken cancellationToken) =>
        dashboardService.GetSummaryAsync(cancellationToken);
}
