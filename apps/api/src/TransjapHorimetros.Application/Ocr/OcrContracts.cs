using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Ocr;

public sealed record OcrWord(string Text, decimal? Confidence);
public sealed record OcrProviderResult(string RawText, string? Model, IReadOnlyList<OcrWord> Words);
public interface IPhotoOcrProvider
{
    string Name { get; }
    Task<OcrProviderResult> AnalyzeAsync(Stream content, string contentType, CancellationToken cancellationToken);
}
public sealed class OcrProviderException(string code, bool transient = false) : Exception(code)
{
    public string Code { get; } = code;
    public bool IsTransient { get; } = transient;
}
public sealed record MeterExtraction(decimal? Value, decimal? Confidence);
public interface IMeterReadingExtractor
{
    MeterExtraction Extract(OcrProviderResult result, MeterUnit? unit);
}
public interface IPhotoOcrRepository
{
    Task<PhotoOcrResult?> StartAsync(PhotoOcrResult result, OcrOptions options, CancellationToken ct);
    Task<IReadOnlyList<PhotoOcrResult>> GetHistoryAsync(Guid photoEvidenceId, CancellationToken ct);
}
public sealed record PhotoOcrResponse(Guid Id, Guid PhotoEvidenceId, PhotoOcrStatus Status,
    decimal? DetectedValue, decimal? Confidence, DateTimeOffset? ProcessedAtServer,
    string Provider, string? ProviderModel, bool? MatchesOperatorValue,
    decimal? AbsoluteDifference, decimal? PercentageDifference, string? ErrorCode,
    string? ErrorMessage, DateTimeOffset CreatedAt);
public interface IPhotoOcrService
{
    Task<PhotoOcrResponse> GetLatestAsync(Guid readingId, CancellationToken ct);
    Task<IReadOnlyList<PhotoOcrResponse>> GetHistoryAsync(Guid readingId, CancellationToken ct);
    Task<PhotoOcrResponse> ProcessAsync(Guid readingId, bool manual, string correlationId, CancellationToken ct);
}
