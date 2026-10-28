namespace TradeImportsFisSimulatorApi.Config;

using System.ComponentModel.DataAnnotations;

public class IuuNotificationTopicConfig
{
    [Required]
    public required string TopicArn { get; init; }
}
