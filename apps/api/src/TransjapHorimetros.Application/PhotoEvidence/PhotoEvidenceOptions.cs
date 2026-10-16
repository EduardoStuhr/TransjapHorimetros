namespace TransjapHorimetros.Application.PhotoEvidence;

public sealed class PhotoEvidenceOptions
{
    public const string SectionName = "Storage:Photos";
    public const long DefaultMaximumFileSizeBytes = 10 * 1024 * 1024;
    public const long AbsoluteMaximumFileSizeBytes = 10 * 1024 * 1024;

    public string Provider { get; set; } = "AzureBlob";

    public string ContainerName { get; set; } = "photo-evidence";

    public long MaximumFileSizeBytes { get; set; } = DefaultMaximumFileSizeBytes;

    public string? ConnectionString { get; set; }

    public string? AccountUri { get; set; }
}
