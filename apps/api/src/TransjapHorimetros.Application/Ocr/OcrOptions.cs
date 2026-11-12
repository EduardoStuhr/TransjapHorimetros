namespace TransjapHorimetros.Application.Ocr;

public sealed class OcrOptions
{
    public string Provider { get; set; } = "Fake";
    public decimal MinimumConfidence { get; set; } = 0.75m;
    public decimal MaximumDifferenceAbsolute { get; set; } = 0.5m;
    public decimal MaximumDifferencePercentage { get; set; } = 5m;
    public int TimeoutSeconds { get; set; } = 15;
    public int MaximumRetries { get; set; } = 2;
    public int ReprocessCooldownSeconds { get; set; } = 60;
    public bool BackgroundProcessingEnabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 5;
    public int MaximumConcurrentProcessing { get; set; } = 2;
    public int FailuresBeforeAlert { get; set; } = 2;
    public AzureOcrOptions Azure { get; set; } = new();
    public void Validate()
    {
        if (MinimumConfidence is < 0 or > 1 || MaximumDifferenceAbsolute < 0
            || MaximumDifferencePercentage < 0 || TimeoutSeconds is < 1 or > 120
            || MaximumRetries is < 0 or > 3 || ReprocessCooldownSeconds is < 1 or > 86400
            || PollIntervalSeconds is < 1 or > 60 || MaximumConcurrentProcessing is < 1 or > 4
            || FailuresBeforeAlert is < 1 or > 10
            || Provider is not ("Fake" or "AzureAiVision"))
            throw new InvalidOperationException("Configuração Ocr inválida.");
    }
}
public sealed class AzureOcrOptions
{
    public string? Endpoint { get; set; }
    public string? Key { get; set; }
}
