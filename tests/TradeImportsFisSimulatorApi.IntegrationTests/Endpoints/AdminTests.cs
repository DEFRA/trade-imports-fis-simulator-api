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
    public async Task NextId_ShouldIncrementLargestCreatedReference()
    {
        using var client = CreateHttpClient();
        var number = await NextUnusedNumber(client);
        await Create(client, Reference(number + 9));
        await Create(client, Reference(number + 41));
        await Create(client, Reference(number + 10));

        var nextId = await GetNextId(client);

        Assert.Equal(Reference(number + 42), nextId);
    }

    [Fact]
    public async Task NextId_AfterReset_ShouldCarryOnFromCounter()
    {
        using var client = CreateHttpClient();
        var number = await NextUnusedNumber(client);
        await Create(client, Reference(number));
        await Reset(client);

        var nextId = await GetNextId(client);

        Assert.Equal(Reference(number + 1), nextId);
    }

    /// <summary>
    /// The counter is kept between tests and across resets, so start above whatever it has already recorded.
    /// </summary>
    private static async Task<long> NextUnusedNumber(HttpClient client)
    {
        var nextId = await GetNextId(client);
        return long.Parse(nextId!.Split('.').Last()) + 1;
    }

    private static string Reference(long number) => $"IUU.GB.2026.{number:D7}";

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
            new { iuuCheckOutcome = "IUUOK" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private sealed record NextIdResponse(string NextId);
}
