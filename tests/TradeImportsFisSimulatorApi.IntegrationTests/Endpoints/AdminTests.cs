using System.Net;
using System.Net.Http.Json;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TradeImportsFisSimulatorApi.IntegrationTests.Endpoints;

public class AdminTests : IntegrationTestBase
{
    [Fact]
    public async Task Reset_ShouldDeleteAllIuuNotifications()
    {
        using var client = CreateHttpClient();
        await Create(client, NewReference());

        var response = await client.PostAsync("/admin/reset", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var count = await GetIuuNotificationsCollection()
            .CountDocumentsAsync(
                FilterDefinition<BsonDocument>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            );
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task NextId_WhenEmpty_ShouldReturnOne()
    {
        using var client = CreateHttpClient();
        await Reset(client);

        var nextId = await GetNextId(client);

        Assert.Equal("1", nextId);
    }

    [Fact]
    public async Task NextId_ShouldIncrementLargestReference()
    {
        using var client = CreateHttpClient();
        await Reset(client);
        await Create(client, "IUU.GB.2026.0000009");
        await Create(client, "IUU.GB.2026.0000041");
        await Create(client, "IUU.GB.2026.0000010");

        var nextId = await GetNextId(client);

        Assert.Equal("IUU.GB.2026.0000042", nextId);
    }

    private static async Task Reset(HttpClient client)
    {
        var response = await client.PostAsync("/admin/reset", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<string?> GetNextId(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<NextIdResponse>(
            "/admin/nextId",
            TestContext.Current.CancellationToken
        );

        return response?.NextId;
    }

    private static async Task Create(HttpClient client, string reference)
    {
        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{reference}",
            new { iuuOption = "IUUOK" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private sealed record NextIdResponse(string NextId);
}
