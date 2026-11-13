using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Dashboard;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Application.Security;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/operations")]
[Authorize]
public sealed class OperationsController(IOperationalService service) : ControllerBase
{
    [HttpPost("anomalies/{id:guid}/review")]
    [Authorize(Policy = Policies.CanReviewAnomalies)]
    public async Task<IActionResult> ReviewAnomaly(Guid id, AnomalyReviewRequest request, CancellationToken ct)
    {
        await service.ReviewAnomalyAsync(id, request, HttpContext.TraceIdentifier, ct);
        return Ok(new { reviewed = true });
    }

    [HttpGet("utilization")]
    [Authorize(Policy = Policies.CanViewDashboard)]
    public Task<UtilizationReport> Utilization([FromQuery] ReadingQuery query, CancellationToken ct) =>
        service.GetUtilizationAsync(query, ct);

    [HttpGet("summary")]
    [Authorize(Policy = Policies.CanViewDashboard)]
    public Task<OperationalSummary> Summary([FromQuery] ReadingQuery query, CancellationToken ct) =>
        service.GetSummaryAsync(query, ct);

    [HttpGet("readings/{id:guid}/review")]
    [Authorize(Policy = Policies.CanReviewAnomalies)]
    public async Task<IActionResult> GetReview(Guid id, CancellationToken ct) =>
        new JsonResult(await service.GetReviewAsync(id, ct));

    [HttpPost("readings/{id:guid}/review")]
    [Authorize(Policy = Policies.CanReviewAnomalies)]
    public async Task<IActionResult> Review(Guid id, ReviewRequest request, CancellationToken ct) =>
        Ok(await service.ReviewAsync(id, request, HttpContext.TraceIdentifier, ct));
}
