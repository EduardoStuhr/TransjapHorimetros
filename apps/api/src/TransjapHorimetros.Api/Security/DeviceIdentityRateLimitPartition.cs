using System.Security.Claims;

namespace TransjapHorimetros.Api.Security;

public static class DeviceIdentityRateLimitPartition
{
    public static string GetKey(HttpContext context) =>
        $"{context.User.FindFirstValue("sub") ?? "anonymous"}:{context.Connection.RemoteIpAddress}";
}
