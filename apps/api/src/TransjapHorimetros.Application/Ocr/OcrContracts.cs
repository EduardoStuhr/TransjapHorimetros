using System.Text.Json.Serialization;
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
public sealed record OcrCandidate(string Token, decimal Value, bool HasUnit);
public sealed record MeterExtraction(decimal? Value, decimal? Confidence, string Reason = "SELECTED",
    IReadOnlyList<OcrCandidate>? Candidates = null);
public interface IMeterReadingExtractor
{
    MeterExtraction Extract(OcrProviderResult result, MeterUnit? unit);
}
public interface IPhotoOcrRepository
{
    async Task<PhotoOcrResult?> GetLatestAsync(Guid photoEvidenceId, CancellationToken ct) =>
        (await GetHistoryAsync(photoEvidenceId, ct)).FirstOrDefault();
    Task<PhotoOcrResult?> GetByRequestIdAsync(Guid requestId, CancellationToken ct) => Task.FromResult<PhotoOcrResult?>(null);
    Task<bool> HasAnomalyAsync(Guid photoId, AnomalyType type, CancellationToken ct) => Task.FromResult(false);
    Task RecoverExpiredAsync(Guid photoId, OcrOptions options, DateTimeOffset now, CancellationToken ct) => Task.CompletedTask;
    Task<PhotoOcrResult?> StartAsync(PhotoOcrResult result, OcrOptions options, CancellationToken ct);
    Task<IReadOnlyList<PhotoOcrResult>> GetHistoryAsync(Guid photoEvidenceId, CancellationToken ct);
}
public sealed record PhotoOcrResponse(Guid Id, Guid PhotoEvidenceId, PhotoOcrStatus Status,
    decimal? DetectedValue, decimal? Confidence, DateTimeOffset? ProcessedAtServer,
    string Provider, string? ProviderModel, bool? MatchesOperatorValue,
    decimal? AbsoluteDifference, decimal? PercentageDifference, string? ErrorCode,
    string? ErrorMessage, DateTimeOffset CreatedAt,
    string? AlgorithmVersion = null, string? ReviewReason = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<OcrCandidate>? Candidates = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RawText = null,
    bool? BelowPreviousReading = null, decimal? PreviousReadingValue = null);
public interface IPhotoOcrService
{
    Task<PhotoOcrResponse> GetLatestAsync(Guid readingId, CancellationToken ct);
    Task<IReadOnlyList<PhotoOcrResponse>> GetHistoryAsync(Guid readingId, CancellationToken ct);
    Task<PhotoOcrResponse> ProcessAsync(Guid readingId, bool manual, string correlationId, CancellationToken ct);
    Task<PhotoOcrResponse> ReprocessAsync(Guid readingId, Guid requestId, string correlationId, CancellationToken ct)
        => ProcessAsync(readingId, true, correlationId, ct);
    async Task NotifyPhotoPersistedAsync(Guid readingId, string correlationId, CancellationToken ct)
        => await ProcessAsync(readingId, false, correlationId, ct);
}
