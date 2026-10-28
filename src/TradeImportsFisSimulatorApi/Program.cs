using System.Diagnostics.CodeAnalysis;
using Amazon.SimpleNotificationService;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MongoDB.Driver;
using MongoDB.Driver.Authentication.AWS;
using Serilog;
using TradeImportsFisSimulatorApi.Admin.Endpoints;
using TradeImportsFisSimulatorApi.Admin.Services;
using TradeImportsFisSimulatorApi.Config;
using TradeImportsFisSimulatorApi.IuuNotifications.Endpoints;
using TradeImportsFisSimulatorApi.IuuNotifications.Services;
using TradeImportsFisSimulatorApi.Utils;
using TradeImportsFisSimulatorApi.Utils.Http;
using TradeImportsFisSimulatorApi.Utils.Logging;
using TradeImportsFisSimulatorApi.Utils.Mongo;

var app = BuildApp(args);
await app.RunAsync();

[ExcludeFromCodeCoverage]
static WebApplication BuildApp(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    ConfigureHost(builder);
    ConfigureServices(builder);

    var app = builder.Build();

    ConfigureMiddleware(app);
    ConfigureEndpoints(app);

    return app;
}

[ExcludeFromCodeCoverage]
static void ConfigureHost(WebApplicationBuilder builder)
{
    builder.Host.UseSerilog(CdpLogging.Configuration);
}

[ExcludeFromCodeCoverage]
static void ConfigureServices(WebApplicationBuilder builder)
{
    var services = builder.Services;
    var configuration = builder.Configuration;

    // Trust material must be loaded before anything creates outbound connections.
    services.LoadCustomTrustStoreFromEnvironment();

    services.AddProblemDetails();
    services.AddValidation();

    services.AddHttpContextAccessor();

    ConfigureHeaderPropagation(services, configuration);
    ConfigureHttpClients(services);
    ConfigureMongo(services, configuration);
    ConfigureAws(services, configuration);

    services.AddHealthChecks();

    // App services
    services.AddSingleton<IIuuNotificationRepository, IuuNotificationRepository>();
    services.AddSingleton<IIuuNotificationPublisher, IuuNotificationPublisher>();
    services.AddSingleton<IMongoResetService, MongoResetService>();
}

[ExcludeFromCodeCoverage]
static void ConfigureAws(IServiceCollection services, IConfiguration configuration)
{
    services
        .AddOptions<IuuNotificationTopicConfig>()
        .Bind(configuration.GetRequiredSection("IuuNotificationTopic"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddDefaultAWSOptions(configuration.GetAWSOptions());
    services.AddAWSService<IAmazonSimpleNotificationService>();
}

[ExcludeFromCodeCoverage]
static void ConfigureHeaderPropagation(IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<TraceHeader>().Bind(configuration).ValidateDataAnnotations().ValidateOnStart();

    var traceHeader = configuration.GetValue<string>("TraceHeader");

    services.AddHeaderPropagation(options =>
    {
        if (!string.IsNullOrWhiteSpace(traceHeader))
        {
            options.Headers.Add(traceHeader);
        }
    });
}

[ExcludeFromCodeCoverage]
static void ConfigureHttpClients(IServiceCollection services)
{
    services.AddTransient<ProxyHttpMessageHandler>();

    // services.AddHttpClientWithTracing<IExampleClient, ExampleClient>();
    // services.AddHttpClientWithProxy<IExternalClient, ExternalClient>();
}

[ExcludeFromCodeCoverage]
static void ConfigureMongo(IServiceCollection services, IConfiguration configuration)
{
    MongoExtensions.Register();
    MongoConventions.Register();

    services
        .AddOptions<MongoConfig>()
        .Bind(configuration.GetRequiredSection("Mongo"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();
}

[ExcludeFromCodeCoverage]
static void ConfigureMiddleware(WebApplication app)
{
    app.UseSerilogRequestLogging();

    app.UseHeaderPropagation();
}

[ExcludeFromCodeCoverage]
static void ConfigureEndpoints(WebApplication app)
{
    app.MapHealthChecks("/health", new HealthCheckOptions());

    app.MapIuuNotificationEndpoints();
    app.MapAdminEndpoints();
}
