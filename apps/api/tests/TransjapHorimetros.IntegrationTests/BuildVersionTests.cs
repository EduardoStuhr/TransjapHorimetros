using System.Net.Http.Json;
using System.Text.Json;

namespace TransjapHorimetros.IntegrationTests;

public sealed class BuildVersionTests(TransjapApiFactory factory) : IClassFixture<TransjapApiFactory>
{
    [Fact]
    public async Task VersionIsPublicAndContainsOnlyAnImmutableCommit()
    {
        using var client = factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/version");
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(value.EnumerateObject());
        Assert.Matches("^[0-9a-f]{40}$", value.GetProperty("commit").GetString()!);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }
}
