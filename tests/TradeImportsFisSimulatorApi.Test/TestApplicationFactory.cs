using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using TradeImportsFisSimulatorApi.Admin.Services;
using TradeImportsFisSimulatorApi.IuuNotifications.Services;

namespace TradeImportsFisSimulatorApi.Test;

/// <summary>
/// Runs the real application with MongoDB and SNS replaced by substitutes.
/// </summary>
public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    public IIuuNotificationRepository Repository { get; } = Substitute.For<IIuuNotificationRepository>();

    public IIuuNotificationPublisher Publisher { get; } = Substitute.For<IIuuNotificationPublisher>();

    public IMongoResetService ResetService { get; } = Substitute.For<IMongoResetService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIuuNotificationRepository>();
            services.AddSingleton(Repository);
            services.RemoveAll<IIuuNotificationPublisher>();
            services.AddSingleton(Publisher);
            services.RemoveAll<IMongoResetService>();
            services.AddSingleton(ResetService);
        });
    }
}
