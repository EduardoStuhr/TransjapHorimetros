using System.Net;
using System.Text;
using TransjapHorimetros.Application.Ocr;
using TransjapHorimetros.Infrastructure.Ocr;

namespace TransjapHorimetros.UnitTests;

public sealed class AzureAiVisionOcrProviderTests
{
    private const string ValidResponse = """
        {"modelVersion":"2023-10-01","readResult":{"blocks":[{"lines":[{"text":"HOURS 1234.5","words":[{"text":"HOURS","confidence":0.99},{"text":"1234.5","confidence":0.91}]}]}]}}
        """;

    [Fact]
    public async Task Analyze_UsesBytesAndKey_ParsesOnlyReadWords()
    {
        var handler = new StubHandler(async request =>
        {
            Assert.Equal("/computervision/imageanalysis:analyze", request.RequestUri!.AbsolutePath);
            Assert.Contains("features=read", request.RequestUri.Query);
            Assert.Equal("test-key", request.Headers.GetValues("Ocp-Apim-Subscription-Key").Single());
            Assert.Equal("application/octet-stream", request.Content!.Headers.ContentType!.MediaType);
            Assert.Equal("image-bytes", await request.Content.ReadAsStringAsync());
            return new(HttpStatusCode.OK) { Content = new StringContent(ValidResponse) };
        });
        var result = await Provider(handler).AnalyzeAsync(new MemoryStream(Encoding.UTF8.GetBytes("image-bytes")), "image/jpeg", default);
        Assert.Equal("HOURS 1234.5", result.RawText);
        Assert.Equal(0.91m, result.Words[1].Confidence);
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    public async Task Analyze_ClassifiesErrorsWithoutLeakingPayload(int status, bool transient)
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            { Content = new StringContent("secret response must not leak") }));
        var ex = await Assert.ThrowsAsync<OcrProviderException>(() => Provider(handler).AnalyzeAsync(new MemoryStream(), "image/png", default));
        Assert.Equal(transient, ex.IsTransient);
        Assert.Equal($"OCR_HTTP_{status}", ex.Code);
        Assert.DoesNotContain("secret", ex.Message);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"readResult\":null}")]
    public async Task Analyze_InvalidResponseIsSanitized(string json)
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }));
        var ex = await Assert.ThrowsAsync<OcrProviderException>(() => Provider(handler).AnalyzeAsync(new MemoryStream(), "image/png", default));
        Assert.Equal("OCR_INVALID_RESPONSE", ex.Code);
    }

    [Fact]
    public async Task Analyze_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Provider(new StubHandler(_ => throw new Exception()))
            .AnalyzeAsync(new MemoryStream(), "image/png", cts.Token));
    }

    [Fact]
    public async Task Analyze_RejectsOversizedStreamBeforeSendingAnyHttpRequest()
    {
        var called = false;
        var provider = Provider(new StubHandler(_ =>
        {
            called = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        using var content = new MemoryStream(new byte[10 * 1024 * 1024 + 1]);
        var error = await Assert.ThrowsAsync<OcrProviderException>(() => provider.AnalyzeAsync(content, "image/jpeg", default));
        Assert.Equal("OCR_IMAGE_TOO_LARGE", error.Code);
        Assert.False(called);
    }

    private static AzureAiVisionOcrProvider Provider(HttpMessageHandler handler) => new(new HttpClient(handler),
        new OcrOptions { Azure = new() { Endpoint = "https://vision.tests.local", Key = "test-key" } });
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
}
