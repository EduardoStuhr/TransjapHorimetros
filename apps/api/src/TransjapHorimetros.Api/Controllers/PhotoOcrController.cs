using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Ocr;
using TransjapHorimetros.Application.Security;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/readings/{readingId:guid}/photo/ocr")]
public sealed class PhotoOcrController(IPhotoOcrService ocr) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.CanRecordReading)]
    public Task<PhotoOcrResponse> Latest(Guid readingId, CancellationToken ct) => ocr.GetLatestAsync(readingId, ct);

    [HttpGet("history")]
    [Authorize(Policy = Policies.CanReviewAnomalies)]
    public Task<IReadOnlyList<PhotoOcrResponse>> History(Guid readingId, CancellationToken ct) => ocr.GetHistoryAsync(readingId, ct);

    [HttpPost("reprocess")]
    [Authorize(Policy = Policies.CanReviewAnomalies)]
    public Task<PhotoOcrResponse> Reprocess(Guid readingId, CancellationToken ct) =>
        ocr.ProcessAsync(readingId, true, HttpContext.TraceIdentifier, ct);
}
