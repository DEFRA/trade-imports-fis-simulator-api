using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace TradeImportsFisSimulatorApi.Test.Config;

public class IuuNotificationTopicConfigTest
{
    [Fact]
    public async Task Startup_WhenTopicNameEmpty_ShouldFailValidation()
    {
        await using var factory = new TestApplicationFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("IuuNotificationTopic:TopicName", "")
        );

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("TopicName", exception.Message);
    }
}
