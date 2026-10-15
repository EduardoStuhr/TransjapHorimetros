using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TransjapHorimetros.Domain.Security;

namespace TransjapHorimetros.IntegrationTests;

public sealed class AuthenticationIntegrationTests(TransjapApiFactory factory)
    : IClassFixture<TransjapApiFactory>
{
    private const string Password = "Test!Password2026";

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAnonymousClient();

        using var response = await client.GetAsync("/api/v1/machines");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokensAndUser()
    {
        await factory.ResetDatabaseAsync();
        var user = await factory.CreateUserAsync(
            "operator.login@tests.local",
            Password,
            true,
            AppRoles.Operator);
        using var client = factory.CreateAnonymousClient();

        var auth = await LoginAsync(client, user.Email!, Password);

        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.Equal(user.Id, auth.User.Id);
        Assert.Contains(AppRoles.Operator, auth.User.Roles);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var user = await factory.CreateUserAsync(
            "wrong.password@tests.local",
            Password,
            true,
            AppRoles.Operator);
        using var client = factory.CreateAnonymousClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = user.Email, password = "Wrong!Password2026" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ForInactiveUser_ReturnsUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var user = await factory.CreateUserAsync(
            "inactive@tests.local",
            Password,
            false,
            AppRoles.Operator);
        using var client = factory.CreateAnonymousClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = user.Email, password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CanResolveQr_ButCannotManageQrOrCreateMachine()
    {
        await factory.ResetDatabaseAsync();
        var user = await factory.CreateUserAsync(
            "operator.authorization@tests.local",
            Password,
            true,
            AppRoles.Operator);
        using var client = factory.CreateAnonymousClient();
        await AuthenticateAsync(client, user.Email!, Password);

        using var resolve = await client.GetAsync("/api/v1/qr-codes/resolve?value=0068");
        using var manageQr = await client.PostAsJsonAsync(
            $"/api/v1/machines/{Guid.NewGuid()}/qr-codes",
            new { token = "0068" });
        using var createMachine = await client.PostAsJsonAsync(
            "/api/v1/machines",
            new { fleetNumber = 9999, model = "Bloqueada", status = "ACTIVE" });

        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, manageQr.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createMachine.StatusCode);
    }

    [Fact]
    public async Task Admin_CanManageQrAndUsers()
    {
        await factory.ResetDatabaseAsync();
        var user = await factory.CreateUserAsync(
            "admin.authorization@tests.local",
            Password,
            true,
            AppRoles.Admin);
        using var client = factory.CreateAnonymousClient();
        await AuthenticateAsync(client, user.Email!, Password);

        var machineId = await GetMachineIdAsync(client, 68);
        using var manageQr = await client.PostAsJsonAsync(
            $"/api/v1/machines/{machineId}/qr-codes",
            new { token = "phase3-admin-qr" });
        using var users = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Created, manageQr.StatusCode);
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
    }

    [Fact]
    public async Task Dashboard_AllowsSupervisor_AndForbidsOperator()
    {
        await factory.ResetDatabaseAsync();
        var supervisor = await factory.CreateUserAsync(
            "supervisor.dashboard@tests.local",
            Password,
            true,
            AppRoles.Supervisor);
        var operatorUser = await factory.CreateUserAsync(
            "operator.dashboard@tests.local",
            Password,
            true,
            AppRoles.Operator);
        using var supervisorClient = factory.CreateAnonymousClient();
        using var operatorClient = factory.CreateAnonymousClient();
        await AuthenticateAsync(supervisorClient, supervisor.Email!, Password);
        await AuthenticateAsync(operatorClient, operatorUser.Email!, Password);

        using var supervisorResponse = await supervisorClient.GetAsync("/api/v1/dashboard/summary");
        using var operatorResponse = await operatorClient.GetAsync("/api/v1/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, supervisorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, operatorResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndRejectsPreviousToken()
    {
        await factory.ResetDatabaseAsync();
        var user = await factory.CreateUserAsync(
            "refresh@tests.local",
            Password,
            true,
            AppRoles.Operator);
        using var client = factory.CreateAnonymousClient();
        var initial = await LoginAsync(client, user.Email!, Password);

        using var rotatedResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = initial.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
        var rotated = (await rotatedResponse.Content.ReadFromJsonAsync<AuthPayload>())!;
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);

        using var reusedResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = initial.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reusedResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        await factory.ResetDatabaseAsync();
        var user = await factory.CreateUserAsync(
            "logout@tests.local",
            Password,
            true,
            AppRoles.Operator);
        using var client = factory.CreateAnonymousClient();
        var auth = await LoginAsync(client, user.Email!, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        using var logout = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        using var refresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsAuthenticatedUser()
    {
        await factory.ResetDatabaseAsync();
        var user = await factory.CreateUserAsync(
            "me@tests.local",
            Password,
            true,
            AppRoles.Manager);
        using var client = factory.CreateAnonymousClient();
        var auth = await LoginAsync(client, user.Email!, Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        using var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(user.Id, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(user.Email, body.RootElement.GetProperty("email").GetString());
        Assert.Contains(
            body.RootElement.GetProperty("roles").EnumerateArray(),
            role => role.GetString() == AppRoles.Manager);
    }

    private static async Task AuthenticateAsync(HttpClient client, string email, string password)
    {
        var auth = await LoginAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }

    private static async Task<AuthPayload> LoginAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthPayload>())!;
    }

    private static async Task<Guid> GetMachineIdAsync(HttpClient client, int fleetNumber)
    {
        using var response = await client.GetAsync($"/api/v1/machines/by-fleet/{fleetNumber}");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private sealed record AuthPayload(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        string RefreshToken,
        UserPayload User);

    private sealed record UserPayload(
        Guid Id,
        string Email,
        string FullName,
        bool IsActive,
        IReadOnlyList<string> Roles,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastLoginAt);
}
