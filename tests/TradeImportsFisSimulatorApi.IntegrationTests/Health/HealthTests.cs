using System.Net;

namespace TradeImportsFisSimulatorApi.IntegrationTests.Health;

public class HealthTests : IntegrationTestBase
{
    [Fact]
    public async Task Health_ShouldBeOk()
    {
        using var client = CreateHttpClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
