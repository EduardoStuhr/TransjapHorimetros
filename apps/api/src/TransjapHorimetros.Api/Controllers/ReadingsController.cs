using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Exceptions;
using TransjapHorimetros.Application.PhotoEvidence;
using TransjapHorimetros.Application.Readings;
using TransjapHorimetros.Application.Security;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/readings")]
public sealed class ReadingsController(
    IReadingService readingService,
    IPhotoEvidenceService photoEvidenceService) : ControllerBase
{
    /// <summary>Lista leituras com filtros e paginação, mais recentes primeiro.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ReadingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<PagedResponse<ReadingResponse>> GetAll(
        [FromQuery] ReadingQuery query,
        CancellationToken cancellationToken) =>
        readingService.GetPageAsync(query, cancellationToken);

    /// <summary>Obtém uma leitura por identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReadingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<ReadingResponse> GetById(Guid id, CancellationToken cancellationToken) =>
        readingService.GetByIdAsync(id, cancellationToken);

    /// <summary>Registra uma leitura de forma idempotente e detecta inconsistências.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.CanRecordReading)]
    [ProducesResponseType(typeof(ReadingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ReadingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReadingResponse>> Create(
        [FromBody] CreateReadingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await readingService.CreateAsync(
            request,
            HttpContext.TraceIdentifier,
            cancellationToken,
            Request.Headers["X-Device-Session"].FirstOrDefault());
        return result.IsExisting
            ? Ok(result.Reading)
            : CreatedAtAction(nameof(GetById), new { id = result.Reading.Id }, result.Reading);
    }

    /// <summary>Envia uma evidência fotográfica imutável para uma leitura existente.</summary>
    [HttpPost("{readingId:guid}/photo")]
    [Authorize(Policy = Policies.CanRecordReading)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PhotoUploadRequestLimits.MaximumMultipartBodyLength)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadRequestLimits.MaximumMultipartBodyLength)]
    [ProducesResponseType(typeof(PhotoEvidenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PhotoEvidenceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<PhotoEvidenceResponse>> UploadPhoto(
        Guid readingId,
        [FromForm] UploadPhotoEvidenceForm form,
        CancellationToken cancellationToken)
    {
        if (form.File is null)
        {
            throw new BadRequestException("O campo file é obrigatório.");
        }

        await using var content = form.File.OpenReadStream();
        var result = await photoEvidenceService.UploadAsync(
            readingId,
            new UploadPhotoEvidenceRequest(
                content,
                form.File.FileName,
                form.File.ContentType,
                form.CapturedAtDevice,
                form.DeviceId,
                form.ClientEventId),
            HttpContext.TraceIdentifier,
            cancellationToken,
            Request.Headers["X-Device-Session"].FirstOrDefault());

        return result.IsExisting
            ? Ok(result.Evidence)
            : CreatedAtAction(
                nameof(GetPhotoMetadata),
                new { readingId },
                result.Evidence);
    }

    /// <summary>Obtém os metadados públicos da evidência sem expor informações internas do Blob.</summary>
    [HttpGet("{readingId:guid}/photo")]
    [Authorize(Policy = Policies.CanRecordReading)]
    [ProducesResponseType(typeof(PhotoEvidenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<PhotoEvidenceResponse> GetPhotoMetadata(
        Guid readingId,
        CancellationToken cancellationToken) =>
        photoEvidenceService.GetMetadataAsync(readingId, cancellationToken);

    /// <summary>Transmite o conteúdo original da evidência pelo canal autenticado da API.</summary>
    [HttpGet("{readingId:guid}/photo/content")]
    [Authorize(Policy = Policies.CanRecordReading)]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhotoContent(
        Guid readingId,
        CancellationToken cancellationToken)
    {
        var content = await photoEvidenceService.OpenContentAsync(
            readingId,
            cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(
            content.Content,
            content.ContentType,
            fileDownloadName: null,
            enableRangeProcessing: false);
    }
}

public sealed record UploadPhotoEvidenceForm(
    IFormFile? File,
    DateTimeOffset? CapturedAtDevice,
    string? DeviceId,
    Guid? ClientEventId);

internal static class PhotoUploadRequestLimits
{
    public const long MaximumMultipartBodyLength = 11 * 1024 * 1024;
}
