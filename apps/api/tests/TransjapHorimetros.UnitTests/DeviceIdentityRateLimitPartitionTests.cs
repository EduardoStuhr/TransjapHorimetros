using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TransjapHorimetros.Api.Security;

namespace TransjapHorimetros.UnitTests;

public sealed class DeviceIdentityRateLimitPartitionTests
{
    [Fact]
    public void AuthenticatedRequest_UsesSubjectAndIpAddress()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "user-123")],
                authenticationType: "test")),
        };
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");

        Assert.Equal("user-123:192.0.2.10", DeviceIdentityRateLimitPartition.GetKey(context));
    }
}
