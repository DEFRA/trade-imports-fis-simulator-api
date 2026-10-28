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
    public async Task NextId_ShouldIncrementLargestStoredReference()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory
            .Repository.GetAllReferencesAsync(Arg.Any<CancellationToken>())
            .Returns(["IUU.GB.2026.0000009", "IUU.GB.2026.0000041"]);
        using var client = factory.CreateClient();

        var response = await client.GetFromJsonAsync<NextIdResponse>("/admin/nextId", cancellationToken);

        Assert.Equal("IUU.GB.2026.0000042", response?.NextId);
    }

    [Fact]
    public async Task NextId_WhenNoReferences_ShouldReturnOne()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory.Repository.GetAllReferencesAsync(Arg.Any<CancellationToken>()).Returns([]);
        using var client = factory.CreateClient();

        var response = await client.GetFromJsonAsync<NextIdResponse>("/admin/nextId", cancellationToken);

        Assert.Equal("1", response?.NextId);
    }
}
