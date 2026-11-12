using TransjapHorimetros.Application.Ocr;

namespace TransjapHorimetros.IntegrationTests;

public sealed class FakePhotoOcrProvider : IPhotoOcrProvider
{
    public string Name => "FakeTests";
    public int Calls { get; private set; }
    public string Scenario { get; set; } = "success";
    public void Reset() { Calls = 0; Scenario = "success"; }
    public async Task<OcrProviderResult> AnalyzeAsync(Stream content, string contentType, CancellationToken ct)
    {
        Calls++;
        switch (Scenario)
        {
            case "timeout": await Task.Delay(Timeout.Infinite, ct); break;
            case "429": throw new OcrProviderException("OCR_HTTP_429", true);
            case "500": throw new OcrProviderException("OCR_HTTP_500", true);
            case "invalid": throw new OcrProviderException("OCR_INVALID_RESPONSE");
            case "ambiguous": return new("HOURS 100\nHOURS 200", "fake", [new("100", 0.95m), new("200", 0.95m)]);
            case "brazilian": return new("HOURS 1.250,5", "fake", [new("1.250,5", 0.95m)]);
            case "missing-confidence": return new("HOURS 100", "fake", [new("100", null)]);
            case "no-value": return new("unreadable", "fake", []);
        }
        var value = Scenario == "divergence" ? "200" : "100";
        return new($"HOURS {value}", "fake", [new(value, Scenario == "low" ? 0.25m : 0.95m)]);
    }
}
