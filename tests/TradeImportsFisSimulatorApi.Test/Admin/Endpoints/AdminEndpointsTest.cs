using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using TradeImportsFisSimulatorApi.IuuNotifications.Models;

namespace TradeImportsFisSimulatorApi.Test.Admin.Endpoints;

public class AdminEndpointsTest
{
    [Fact]
    public async Task Reset_ShouldResetMongoAndReturnNoContent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/admin/reset", null, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await factory.ResetService.Received(1).ResetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NextId_ShouldIncrementLastCountedReference()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory.ReferenceCounter.GetLastReferenceAsync(Arg.Any<CancellationToken>()).Returns("IUU.GB.2026.0000041");
        using var client = factory.CreateClient();

        var response = await client.GetFromJsonAsync<NextIdResponse>("/admin/nextId", cancellationToken);

        Assert.Equal("IUU.GB.2026.0000042", response?.NextId);
    }

    [Fact]
    public async Task NextId_WhenNothingCounted_ShouldReturnOne()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory.ReferenceCounter.GetLastReferenceAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        using var client = factory.CreateClient();

        var response = await client.GetFromJsonAsync<NextIdResponse>("/admin/nextId", cancellationToken);

        Assert.Equal("1", response?.NextId);
    }
}
