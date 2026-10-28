using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.SimpleNotificationService;
using Microsoft.Extensions.Options;
using TradeImportsFisSimulatorApi.Config;

namespace TradeImportsFisSimulatorApi.IuuNotifications.Services;

public interface IIuuNotificationTopicArnResolver
{
    Task<string> GetTopicArnAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the topic ARN from the configured topic name, the region of the SNS client and the account the service
/// is running in, so only the topic name needs configuring in each environment. GetCallerIdentity is used for the
/// account as it needs no IAM permissions, unlike looking the topic up with ListTopics.
/// </summary>
public class IuuNotificationTopicArnResolver(
    IAmazonSecurityTokenService sts,
    IAmazonSimpleNotificationService sns,
    IOptions<IuuNotificationTopicConfig> config,
    ILogger<IuuNotificationTopicArnResolver> logger
) : IIuuNotificationTopicArnResolver
{
    private string? _topicArn;

    public async Task<string> GetTopicArnAsync(CancellationToken cancellationToken = default)
    {
        // Not locked: concurrent first calls resolve the same ARN, and a failed call is retried next time.
        if (_topicArn is not null)
        {
            return _topicArn;
        }

        var region =
            sns.Config.RegionEndpoint?.SystemName
            ?? sns.Config.AuthenticationRegion
            ?? throw new InvalidOperationException("Unable to determine the AWS region for the SNS client");

        var identity = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest(), cancellationToken);

        _topicArn = $"arn:aws:sns:{region}:{identity.Account}:{config.Value.TopicName}";
        logger.LogInformation("Resolved IUU notification topic ARN {TopicArn}", _topicArn);

        return _topicArn;
    }
}
