using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Options;
using TradeImportsFisSimulatorApi.IuuNotifications.Models;
using TradeImportsFisSimulatorApi.Utils.Logging;

namespace TradeImportsFisSimulatorApi.IuuNotifications.Services;

public interface IIuuNotificationPublisher
{
    Task PublishAsync(IuuNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Publishes IUU notifications as the Fish Import Service would. The message body is the notification itself,
/// which the Trade Imports Processor consumes. No MessageType attribute is sent, as the processor's consumer is
/// bound to a single message type and would fail to resolve one it does not know.
/// </summary>
public class IuuNotificationPublisher(
    IAmazonSimpleNotificationService sns,
    IHttpContextAccessor httpContextAccessor,
    IOptions<TraceHeader> traceHeaderOptions,
    IIuuNotificationTopicArnResolver topicArnResolver,
    ILogger<IuuNotificationPublisher> logger
) : IIuuNotificationPublisher
{
    private const string DataTypeString = "String";

    public async Task PublishAsync(IuuNotification notification, CancellationToken cancellationToken = default)
    {
        var messageAttributes = new Dictionary<string, MessageAttributeValue>();
        AddTraceIdIfPresent(messageAttributes);

        var request = new PublishRequest
        {
            TopicArn = await topicArnResolver.GetTopicArnAsync(cancellationToken),
            Message = JsonSerializer.Serialize(notification),
            MessageAttributes = messageAttributes,
        };

        var response = await sns.PublishAsync(request, cancellationToken);

        logger.LogInformation(
            "Published IUU notification {Reference} with message id {MessageId}",
            notification.Reference,
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
