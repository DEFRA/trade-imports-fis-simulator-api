using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Defra.TradeImportsDataApi.Domain.Events;
using Microsoft.Extensions.Options;
using TradeImportsFisSimulatorApi.Config;
using TradeImportsFisSimulatorApi.Utils.Logging;

namespace TradeImportsFisSimulatorApi.IuuNotifications.Services;

public interface IIuuNotificationPublisher
{
    Task PublishAsync(ResourceEvent<IuuNotificationEvent> resourceEvent, CancellationToken cancellationToken = default);
}

public class IuuNotificationPublisher(
    IAmazonSimpleNotificationService sns,
    IHttpContextAccessor httpContextAccessor,
    IOptions<TraceHeader> traceHeaderOptions,
    IOptions<IuuNotificationTopicConfig> config,
    ILogger<IuuNotificationPublisher> logger
) : IIuuNotificationPublisher
{
    private const string DataTypeString = "String";
    private const string MessageTypeHeader = "MessageType";

    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Matches the MessageType the Trade Imports Data API sends, so consumers can resolve the payload type.
    private static readonly string s_messageType = Regex.Replace(
        typeof(ResourceEvent<IuuNotificationEvent>).AssemblyQualifiedName!,
        @"\, (Version|Culture|PublicKeyToken)\=([\w\d.]+)",
        string.Empty,
        RegexOptions.None,
        TimeSpan.FromSeconds(2)
    );

    public async Task PublishAsync(
        ResourceEvent<IuuNotificationEvent> resourceEvent,
        CancellationToken cancellationToken = default
    )
    {
        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            [MessageTypeHeader] = new() { DataType = DataTypeString, StringValue = s_messageType },
            [nameof(resourceEvent.ResourceType)] = new()
            {
                DataType = DataTypeString,
                StringValue = resourceEvent.ResourceType,
            },
            [nameof(resourceEvent.ResourceId)] = new()
            {
                DataType = DataTypeString,
                StringValue = resourceEvent.ResourceId,
            },
        };

        AddTraceIdIfPresent(messageAttributes);

        var request = new PublishRequest
        {
            TopicArn = config.Value.TopicArn,
            Message = JsonSerializer.Serialize(resourceEvent, s_serializerOptions),
            MessageAttributes = messageAttributes,
        };

        var response = await sns.PublishAsync(request, cancellationToken);

        logger.LogInformation(
            "Published {Operation} event for IUU notification {Reference} with message id {MessageId}",
            resourceEvent.Operation,
            resourceEvent.ResourceId,
            response.MessageId
        );
    }

    private void AddTraceIdIfPresent(Dictionary<string, MessageAttributeValue> messageAttributes)
    {
        var traceHeaderName = traceHeaderOptions.Value.Name;
        var traceId = httpContextAccessor.HttpContext?.Request.Headers[traceHeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            messageAttributes[traceHeaderName] = new MessageAttributeValue
            {
                DataType = DataTypeString,
                StringValue = traceId,
            };
        }
    }
}
