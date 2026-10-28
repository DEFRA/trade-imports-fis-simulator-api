using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Iuu;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TradeImportsFisSimulatorApi.IuuNotifications.Services;
using TradeImportsFisSimulatorApi.Utils.Logging;

namespace TradeImportsFisSimulatorApi.Test.IuuNotifications.Services;

public class IuuNotificationPublisherTest
{
    private const string TopicArn = "arn:aws:sns:eu-west-2:000000000000:trade_imports_iuunotification_sim_upserted";
    private const string TraceHeaderName = "x-cdp-request-id";
    private const string Reference = "IUU.GB.2026.0000001";

    private readonly IAmazonSimpleNotificationService _sns = Substitute.For<IAmazonSimpleNotificationService>();
    private readonly IIuuNotificationTopicArnResolver _topicArnResolver =
        Substitute.For<IIuuNotificationTopicArnResolver>();
    private readonly HttpContextAccessor _httpContextAccessor = new();
    private PublishRequest? _request;

    public IuuNotificationPublisherTest()
    {
        _topicArnResolver.GetTopicArnAsync(Arg.Any<CancellationToken>()).Returns(TopicArn);
        _sns.PublishAsync(Arg.Do<PublishRequest>(r => _request = r), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "message-id" });
    }

    [Fact]
    public async Task PublishAsync_ShouldPublishCamelCaseEventToTopic()
    {
        await CreatePublisher().PublishAsync(CreateEvent(), TestContext.Current.CancellationToken);

        Assert.NotNull(_request);
        Assert.Equal(TopicArn, _request.TopicArn);

        using var message = JsonDocument.Parse(_request.Message);
        var root = message.RootElement;
        Assert.Equal(Reference, root.GetProperty("resourceId").GetString());
        Assert.Equal(ResourceEventOperations.Created, root.GetProperty("operation").GetString());
        Assert.Equal(
            "IUUOK",
            root.GetProperty("resource").GetProperty("iuuNotification").GetProperty("iuuOption").GetString()
        );
    }

    [Fact]
    public async Task PublishAsync_ShouldAddResourceAndMessageTypeAttributes()
    {
        await CreatePublisher().PublishAsync(CreateEvent(), TestContext.Current.CancellationToken);

        Assert.NotNull(_request);
        Assert.Equal(
            ResourceEventResourceTypes.IuuNotification,
            _request.MessageAttributes["ResourceType"].StringValue
        );
        Assert.Equal(Reference, _request.MessageAttributes["ResourceId"].StringValue);

        var messageType = _request.MessageAttributes["MessageType"].StringValue;
        Assert.StartsWith("Defra.TradeImportsDataApi.Domain.Events.ResourceEvent`1", messageType);
        Assert.Contains("Defra.TradeImportsDataApi.Domain.Events.IuuNotificationEvent", messageType);
        Assert.DoesNotContain("Version=", messageType);
        Assert.DoesNotContain("PublicKeyToken=", messageType);
    }

    [Fact]
    public async Task PublishAsync_WhenRequestHasTraceHeader_ShouldAddTraceIdAttribute()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[TraceHeaderName] = "trace-123";
        _httpContextAccessor.HttpContext = httpContext;

        await CreatePublisher().PublishAsync(CreateEvent(), TestContext.Current.CancellationToken);

        Assert.NotNull(_request);
        Assert.Equal("trace-123", _request.MessageAttributes[TraceHeaderName].StringValue);
    }

    [Fact]
    public async Task PublishAsync_WhenRequestHasNoTraceHeader_ShouldNotAddTraceIdAttribute()
    {
        _httpContextAccessor.HttpContext = new DefaultHttpContext();

        await CreatePublisher().PublishAsync(CreateEvent(), TestContext.Current.CancellationToken);

        Assert.NotNull(_request);
        Assert.False(_request.MessageAttributes.ContainsKey(TraceHeaderName));
    }

    [Fact]
    public async Task PublishAsync_WhenNoHttpContext_ShouldNotAddTraceIdAttribute()
    {
        await CreatePublisher().PublishAsync(CreateEvent(), TestContext.Current.CancellationToken);

        Assert.NotNull(_request);
        Assert.False(_request.MessageAttributes.ContainsKey(TraceHeaderName));
    }

    private IuuNotificationPublisher CreatePublisher() =>
        new(
            _sns,
            _httpContextAccessor,
            Options.Create(new TraceHeader { Name = TraceHeaderName }),
            _topicArnResolver,
            NullLogger<IuuNotificationPublisher>.Instance
        );

    private static ResourceEvent<IuuNotificationEvent> CreateEvent()
    {
        var now = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

        return new ResourceEvent<IuuNotificationEvent>
        {
            ResourceId = Reference,
            ResourceType = ResourceEventResourceTypes.IuuNotification,
            Operation = ResourceEventOperations.Created,
            Etag = "etag-1",
            Timestamp = now,
            Resource = new IuuNotificationEvent
            {
                Id = Reference,
                Etag = "etag-1",
                Created = now,
                Updated = now,
                IuuNotification = new IuuNotification
                {
                    Reference = Reference,
                    LastUpdated = now,
                    IuuOption = "IUUOK",
                },
            },
        };
    }
}
