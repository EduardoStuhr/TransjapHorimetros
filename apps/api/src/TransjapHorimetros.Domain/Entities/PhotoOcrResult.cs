using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Entities;

public sealed class PhotoOcrResult
{
    private PhotoOcrResult() { }
    public PhotoOcrResult(Guid photoEvidenceId, string provider, bool isInitial,
        DateTimeOffset createdAt, string correlationId, Guid? actorUserId)
    {
        Id = Guid.NewGuid(); PhotoEvidenceId = photoEvidenceId; Provider = provider;
        IsInitial = isInitial; CreatedAt = createdAt; CorrelationId = correlationId[..Math.Min(100, correlationId.Length)];
        ActorUserId = actorUserId; Status = PhotoOcrStatus.Processing;
    }
    public Guid Id { get; private set; }
    public Guid PhotoEvidenceId { get; private set; }
    public string Provider { get; private set; } = "";
    public string? ProviderModel { get; private set; }
    public string? RawText { get; private set; }
    public decimal? DetectedValue { get; private set; }
    public decimal? Confidence { get; private set; }
    public PhotoOcrStatus Status { get; private set; }
    public DateTimeOffset? ProcessedAtServer { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public long? ProcessingDurationMs { get; private set; }
    public string CorrelationId { get; private set; } = "";
    public Guid? ActorUserId { get; private set; }
    public bool IsInitial { get; private set; }
    public decimal? AbsoluteDifference { get; private set; }
    public decimal? PercentageDifference { get; private set; }
    public bool? MatchesOperatorValue { get; private set; }

    public void Complete(PhotoOcrStatus status, string? text, string? model, decimal? value,
        decimal? confidence, decimal? absoluteDifference, decimal? percentageDifference,
        bool? matches, DateTimeOffset now, long duration, string? errorCode = null)
    {
        if (Status != PhotoOcrStatus.Processing || status is PhotoOcrStatus.Pending or PhotoOcrStatus.Processing)
            throw new InvalidOperationException("O resultado OCR finalizado é imutável.");
        Status = status; RawText = text?[..Math.Min(4000, text.Length)];
        ProviderModel = model?[..Math.Min(100, model.Length)]; DetectedValue = value;
        Confidence = confidence is >= 0 and <= 1 ? confidence : null;
        AbsoluteDifference = absoluteDifference; PercentageDifference = percentageDifference;
        MatchesOperatorValue = matches; ProcessedAtServer = now; ProcessingDurationMs = duration;
        ErrorCode = errorCode;
        ErrorMessage = errorCode is null ? null : "Não foi possível processar a imagem. A evidência permanece válida.";
    }
}
