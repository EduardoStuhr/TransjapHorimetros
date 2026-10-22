using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace TransjapHorimetros.IntegrationTests;

public sealed class TransjapDeviceIdentityFactory : TransjapApiFactory
{
    private readonly TestTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    public TestTimeProvider IdentityTimeProvider => _timeProvider;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(_timeProvider);
        });
    }
}
