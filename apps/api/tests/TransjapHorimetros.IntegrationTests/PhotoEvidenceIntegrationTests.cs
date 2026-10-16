using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TransjapHorimetros.Domain.Security;

namespace TransjapHorimetros.IntegrationTests;

public sealed class PhotoEvidenceIntegrationTests(TransjapApiFactory factory)
    : IClassFixture<TransjapApiFactory>
{
    private const string Password = "Test!Password2026";

    [Fact]
    public async Task Upload_WithoutToken_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAnonymousClient();
        using var form = CreatePhotoForm(CreateJpeg(), "image/jpeg");

        using var response = await client.PostAsync(
            $"/api/v1/readings/{Guid.NewGuid()}/photo",
            form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(AppRoles.Operator)]
    [InlineData(AppRoles.Admin)]
    public async Task Upload_ValidPhoto_ReturnsCreatedForAuthorizedRole(string role)
    {
        await factory.ResetDatabaseAsync();
        using var client = await CreateAuthenticatedClientAsync(role);
        var readingId = await CreateReadingAsync(client);
        using var form = CreatePhotoForm(CreateJpeg(), "image/jpeg");

        using var response = await client.PostAsync(
            $"/api/v1/readings/{readingId}/photo",
            form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ForMissingReading_ReturnsNotFound()
    {
        await factory.ResetDatabaseAsync();
        using var client = await CreateAuthenticatedClientAsync(AppRoles.Operator);
        using var form = CreatePhotoForm(CreateJpeg(), "image/jpeg");

        using var response = await client.PostAsync(
            $"/api/v1/readings/{Guid.NewGuid()}/photo",
            form);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_InvalidFile_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        using var client = await CreateAuthenticatedClientAsync(AppRoles.Operator);
        var readingId = await CreateReadingAsync(client);
        using var form = CreatePhotoForm("<html>not a photo</html>"u8.ToArray(), "image/jpeg");

        using var response = await client.PostAsync(
            $"/api/v1/readings/{readingId}/photo",
            form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_FileAboveLimit_ReturnsPayloadTooLarge()
    {
        await factory.ResetDatabaseAsync();
        using var client = await CreateAuthenticatedClientAsync(AppRoles.Operator);
        var readingId = await CreateReadingAsync(client);
        using var form = CreatePhotoForm(new byte[(10 * 1024 * 1024) + 1], "image/jpeg");

        using var response = await client.PostAsync(
            $"/api/v1/readings/{readingId}/photo",
            form);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateUpload_ReturnsExistingMetadataAndContent()
    {
        await factory.ResetDatabaseAsync();
        using var client = await CreateAuthenticatedClientAsync(AppRoles.Operator);
        var readingId = await CreateReadingAsync(client);
        var clientEventId = Guid.NewGuid();

        using var firstForm = CreatePhotoForm(CreateJpeg(), "image/jpeg", clientEventId);
        using var first = await client.PostAsync(
            $"/api/v1/readings/{readingId}/photo",
            firstForm);
        var firstJson = await first.Content.ReadFromJsonAsync<JsonElement>();

        using var secondForm = CreatePhotoForm(CreateJpeg(), "image/jpeg", clientEventId);
        using var second = await client.PostAsync(
            $"/api/v1/readings/{readingId}/photo",
            secondForm);
        var secondJson = await second.Content.ReadFromJsonAsync<JsonElement>();
        using var metadata = await client.GetAsync($"/api/v1/readings/{readingId}/photo");
        var metadataJson = await metadata.Content.ReadFromJsonAsync<JsonElement>();
        using var content = await client.GetAsync($"/api/v1/readings/{readingId}/photo/content");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstJson.GetProperty("id").GetGuid(), secondJson.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, metadata.StatusCode);
        Assert.False(metadataJson.TryGetProperty("storageObjectKey", out _));
        Assert.False(metadataJson.TryGetProperty("storageContainer", out _));
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("image/jpeg", content.Content.Headers.ContentType?.MediaType);
        Assert.Equal(CreateJpeg(), await content.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Upload_UserWithoutOperationalRole_ReturnsForbidden()
    {
        await factory.ResetDatabaseAsync();
        using var operatorClient = await CreateAuthenticatedClientAsync(AppRoles.Operator);
        var readingId = await CreateReadingAsync(operatorClient);
        using var unauthorizedRoleClient = await CreateAuthenticatedClientAsync("AUDITOR");
        using var form = CreatePhotoForm(CreateJpeg(), "image/jpeg");

        using var response = await unauthorizedRoleClient.PostAsync(
            $"/api/v1/readings/{readingId}/photo",
            form);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<Guid> CreateReadingAsync(HttpClient client)
    {
        using var machinesResponse = await client.GetAsync("/api/v1/machines?page=1&pageSize=1");
        machinesResponse.EnsureSuccessStatusCode();
        var machines = await machinesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var machineId = machines.GetProperty("items")[0].GetProperty("id").GetGuid();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/readings",
            new
            {
                machineId,
                value = 100m,
                readingType = "INTERMEDIATE",
                capturedAtDevice = DateTimeOffset.UtcNow,
                clientEventId = Guid.NewGuid(),
            });
        response.EnsureSuccessStatusCode();
        var reading = await response.Content.ReadFromJsonAsync<JsonElement>();
        return reading.GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string role)
    {
        var user = await factory.CreateUserAsync(
            $"photo.{role.ToLowerInvariant()}.{Guid.NewGuid():N}@tests.local",
            Password,
            true,
            role);
        var client = factory.CreateAnonymousClient();
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = user.Email, password = Password });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            auth.GetProperty("accessToken").GetString());
        return client;
    }

    private static MultipartFormDataContent CreatePhotoForm(
        byte[] content,
        string contentType,
        Guid? clientEventId = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", "proof.jpg");
        form.Add(new StringContent((clientEventId ?? Guid.NewGuid()).ToString()), "clientEventId");
        form.Add(new StringContent(DateTimeOffset.UtcNow.ToString("O")), "capturedAtDevice");
        form.Add(new StringContent("integration-test-device"), "deviceId");
        return form;
    }

    private static byte[] CreateJpeg() =>
    [
        0xff, 0xd8,
        0xff, 0xc0, 0x00, 0x11, 0x08,
        0x00, 0x01,
        0x00, 0x02,
        0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00,
        0xff, 0xd9,
    ];
}
