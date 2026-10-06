using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Common;
using TransjapHorimetros.Application.Readings;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/readings")]
public sealed class ReadingsController(IReadingService readingService) : ControllerBase
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
            cancellationToken);
        return result.IsExisting
            ? Ok(result.Reading)
            : CreatedAtAction(nameof(GetById), new { id = result.Reading.Id }, result.Reading);
    }
}
