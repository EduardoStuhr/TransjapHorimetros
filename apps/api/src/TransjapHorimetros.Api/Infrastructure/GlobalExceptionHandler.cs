using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Exceptions;

namespace TransjapHorimetros.Api.Infrastructure;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ApplicationValidationException => (
                StatusCodes.Status422UnprocessableEntity,
                "Validation error",
                exception.Message),
            EntityNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                exception.Message),
            ConflictException or UniqueConstraintException => (
                StatusCodes.Status409Conflict,
                "Conflict",
                exception.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "Ocorreu um erro inesperado ao processar a solicitação."),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Falha inesperada ao processar {Path}.", httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(
                "Solicitação rejeitada com {StatusCode}: {ErrorMessage}",
                status,
                exception.Message);
        }

        httpContext.Response.StatusCode = status;
        ProblemDetails problemDetails = exception is ApplicationValidationException validationException
            ? new HttpValidationProblemDetails(validationException.Errors)
            : new ProblemDetails();
        problemDetails.Status = status;
        problemDetails.Title = title;
        problemDetails.Detail = detail;
        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        problemDetails.Extensions["correlationId"] = httpContext.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }
}
