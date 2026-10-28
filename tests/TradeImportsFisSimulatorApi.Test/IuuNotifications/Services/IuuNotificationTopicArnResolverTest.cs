using Amazon;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.SimpleNotificationService;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TradeImportsFisSimulatorApi.Config;
using TradeImportsFisSimulatorApi.IuuNotifications.Services;

namespace TradeImportsFisSimulatorApi.Test.IuuNotifications.Services;

public class IuuNotificationTopicArnResolverTest
{
    private const string TopicName = "trade_imports_iuunotification_sim_upserted";

    private readonly IAmazonSecurityTokenService _sts = Substitute.For<IAmazonSecurityTokenService>();
    private readonly IAmazonSimpleNotificationService _sns = Substitute.For<IAmazonSimpleNotificationService>();

    public IuuNotificationTopicArnResolverTest()
    {
        _sts.GetCallerIdentityAsync(Arg.Any<GetCallerIdentityRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetCallerIdentityResponse { Account = "332499610595" });
    }

    [Fact]
    public async Task GetTopicArnAsync_ShouldBuildArnFromRegionAccountAndTopicName()
    {
        _sns.Config.Returns(new AmazonSimpleNotificationServiceConfig { RegionEndpoint = RegionEndpoint.EUWest2 });

        var arn = await CreateResolver().GetTopicArnAsync(TestContext.Current.CancellationToken);

        Assert.Equal($"arn:aws:sns:eu-west-2:332499610595:{TopicName}", arn);
    }

    [Fact]
    public async Task GetTopicArnAsync_WhenUsingServiceUrl_ShouldUseAuthenticationRegion()
    {
        // Locally the client points at floci with a ServiceURL, so it has no RegionEndpoint.
        _sns.Config.Returns(
            new AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = "http://localhost:4566",
                AuthenticationRegion = "eu-west-1",
            }
        );

        var arn = await CreateResolver().GetTopicArnAsync(TestContext.Current.CancellationToken);

        Assert.Equal($"arn:aws:sns:eu-west-1:332499610595:{TopicName}", arn);
    }

    [Fact]
    public async Task GetTopicArnAsync_ShouldOnlyCallStsOnce()
    {
        _sns.Config.Returns(new AmazonSimpleNotificationServiceConfig { RegionEndpoint = RegionEndpoint.EUWest2 });
        var resolver = CreateResolver();

        var first = await resolver.GetTopicArnAsync(TestContext.Current.CancellationToken);
        var second = await resolver.GetTopicArnAsync(TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
        await _sts.Received(1)
            .GetCallerIdentityAsync(Arg.Any<GetCallerIdentityRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTopicArnAsync_WhenStsFails_ShouldRetryOnNextCall()
    {
        _sns.Config.Returns(new AmazonSimpleNotificationServiceConfig { RegionEndpoint = RegionEndpoint.EUWest2 });
        _sts.GetCallerIdentityAsync(Arg.Any<GetCallerIdentityRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new AmazonSecurityTokenServiceException("unavailable"),
                _ => Task.FromResult(new GetCallerIdentityResponse { Account = "332499610595" })
            );
        var resolver = CreateResolver();

        await Assert.ThrowsAsync<AmazonSecurityTokenServiceException>(() =>
            resolver.GetTopicArnAsync(TestContext.Current.CancellationToken)
        );
        var arn = await resolver.GetTopicArnAsync(TestContext.Current.CancellationToken);

        Assert.Equal($"arn:aws:sns:eu-west-2:332499610595:{TopicName}", arn);
    }

    private IuuNotificationTopicArnResolver CreateResolver() =>
        new(
            _sts,
            _sns,
            Options.Create(new IuuNotificationTopicConfig { TopicName = TopicName }),
            NullLogger<IuuNotificationTopicArnResolver>.Instance
        );
}
