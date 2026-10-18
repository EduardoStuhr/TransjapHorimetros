using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using TransjapHorimetros.Application.Ocr;

namespace TransjapHorimetros.Infrastructure.Ocr;

public sealed class AzureAiVisionOcrProvider(HttpClient client, OcrOptions options) : IPhotoOcrProvider
{
    private readonly TokenCredential credential = new DefaultAzureCredential();
    public string Name => "AzureAiVision";
    public async Task<OcrProviderResult> AnalyzeAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(options.Azure.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != "https" || !string.IsNullOrEmpty(endpoint.UserInfo)
            || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
            throw new OcrProviderException("OCR_CONFIGURATION_INVALID");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(endpoint, "/computervision/imageanalysis:analyze?api-version=2024-02-01&features=read"));
        if (!string.IsNullOrWhiteSpace(options.Azure.Key))
            request.Headers.Add("Ocp-Apim-Subscription-Key", options.Azure.Key);
        else
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        // The caller owns the original stream. Buffer only the bounded photo, never a public URL.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        request.Content = new ByteArrayContent(buffer.ToArray());
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        HttpResponseMessage response;
        try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        catch (HttpRequestException) { throw new OcrProviderException("OCR_NETWORK_ERROR", true); }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new OcrProviderException($"OCR_HTTP_{(int)response.StatusCode}",
                    response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500);
            // Bound the external response before parsing; never persist or log the complete payload.
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var jsonBytes = new MemoryStream();
            var chunk = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (jsonBytes.Length + read > 1024 * 1024) throw new OcrProviderException("OCR_RESPONSE_TOO_LARGE");
                jsonBytes.Write(chunk, 0, read);
            }
            try
            {
                using var json = JsonDocument.Parse(jsonBytes.ToArray());
                var root = json.RootElement;
                var lines = root.GetProperty("readResult").GetProperty("blocks").EnumerateArray()
                    .SelectMany(b => b.GetProperty("lines").EnumerateArray()).ToArray();
                var text = string.Join('\n', lines.Select(l => l.GetProperty("text").GetString()));
                var words = lines.SelectMany(l => l.GetProperty("words").EnumerateArray())
                    .Select(w => new OcrWord(w.GetProperty("text").GetString() ?? "",
                        w.TryGetProperty("confidence", out var c) && c.TryGetDecimal(out var confidence)
                            && confidence is >= 0 and <= 1 ? confidence : null)).ToArray();
                return new(OcrText.Limit(text), root.GetProperty("modelVersion").GetString(), words);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            { throw new OcrProviderException("OCR_INVALID_RESPONSE"); }
        }
    }
}
