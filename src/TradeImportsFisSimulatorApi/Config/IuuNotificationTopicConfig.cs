namespace TradeImportsFisSimulatorApi.Config;

using System.ComponentModel.DataAnnotations;

public class IuuNotificationTopicConfig
{
    /// <summary>
    /// Name of the topic events are published to. The ARN is built from this and the account the service runs in.
    /// </summary>
    [Required]
    public string TopicName { get; init; } = string.Empty;
}
