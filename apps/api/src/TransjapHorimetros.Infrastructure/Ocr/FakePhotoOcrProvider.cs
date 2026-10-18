using TransjapHorimetros.Application.Ocr;

namespace TransjapHorimetros.Infrastructure.Ocr;

// Local development only: deliberately returns no fabricated meter reading.
public sealed class FakePhotoOcrProvider : IPhotoOcrProvider
{
    public string Name => "Fake";
    public Task<OcrProviderResult> AnalyzeAsync(Stream content, string contentType, CancellationToken cancellationToken) =>
        Task.FromResult(new OcrProviderResult("", "local-no-detection", []));
}
