namespace TransjapHorimetros.Application.Ocr;

public sealed class OcrProcessingGate(int maximumConcurrency = 2) : IDisposable
{
    private readonly SemaphoreSlim slots = new(maximumConcurrency, maximumConcurrency);
    public Task EnterAsync(CancellationToken ct) => slots.WaitAsync(ct);
    public void Exit() => slots.Release();
    public void Dispose() => slots.Dispose();
}
