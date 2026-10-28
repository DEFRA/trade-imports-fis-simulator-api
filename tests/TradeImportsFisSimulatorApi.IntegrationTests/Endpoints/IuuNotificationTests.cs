using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.SQS.Model;

namespace TradeImportsFisSimulatorApi.IntegrationTests.Endpoints;

public class IuuNotificationTests : SqsTestBase
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Post_WhenNew_ShouldPersistAndPublishIuuNotification()
    {
        var reference = NewReference();
        var traceId = Guid.NewGuid().ToString();
        using var client = CreateHttpClient();
        client.DefaultRequestHeaders.Add(TraceHeader, traceId);

        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{reference}",
            new { iuuCheckOutcome = "IUUOK" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var document = await FindIuuNotification(reference);
        Assert.NotNull(document);
        Assert.Equal("IUUOK", document["iuuNotification"]["iuuCheckOutcome"].AsString);

        var message = await WaitForMessage(reference, "IUUOK");
        using var body = JsonDocument.Parse(message.Body);
        Assert.Equal(
            ["reference", "lastUpdated", "iuuCheckOutcome"],
            body.RootElement.EnumerateObject().Select(p => p.Name)
        );

        var notification = Deserialize(message);
        Assert.Equal(reference, notification.Reference);
        Assert.Equal(document["iuuNotification"]["lastUpdated"].ToUniversalTime(), notification.LastUpdated);

        Assert.False(message.MessageAttributes.ContainsKey("MessageType"));
        Assert.Equal(traceId, message.MessageAttributes[TraceHeader].StringValue);
    }

    [Fact]
    public async Task Post_WhenIuuCheckOutcomeMissing_ShouldBeBadRequest()
    {
        var reference = NewReference();
        using var client = CreateHttpClient();

        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{reference}",
            new { },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await FindIuuNotification(reference));
    }

    [Fact]
    public async Task Post_WhenAlreadyExists_ShouldBeConflict()
    {
        var reference = NewReference();
        using var client = CreateHttpClient();
        await Create(client, reference, "IUUOK");

        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{reference}",
            new { iuuCheckOutcome = "IUUNA" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var document = await FindIuuNotification(reference);
        Assert.Equal("IUUOK", document!["iuuNotification"]["iuuCheckOutcome"].AsString);
    }

    [Fact]
    public async Task Put_WhenExists_ShouldPersistAndPublishUpdatedIuuNotification()
    {
        var reference = NewReference();
        var traceId = Guid.NewGuid().ToString();
        using var client = CreateHttpClient();
        await Create(client, reference, "IUUOK");
        var created = await FindIuuNotification(reference);

        client.DefaultRequestHeaders.Add(TraceHeader, traceId);
        var response = await client.PutAsJsonAsync(
            $"/iuunotification/{reference}",
            new { iuuCheckOutcome = "IUUNotCompliant" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await FindIuuNotification(reference);
        Assert.NotNull(updated);
        Assert.Equal("IUUNotCompliant", updated["iuuNotification"]["iuuCheckOutcome"].AsString);
        Assert.True(
            updated["iuuNotification"]["lastUpdated"].ToUniversalTime()
                > created!["iuuNotification"]["lastUpdated"].ToUniversalTime()
        );
        Assert.Equal(created["created"].ToUniversalTime(), updated["created"].ToUniversalTime());

        var message = await WaitForMessage(reference, "IUUNotCompliant");
        var notification = Deserialize(message);
        Assert.Equal(updated["iuuNotification"]["lastUpdated"].ToUniversalTime(), notification.LastUpdated);
        Assert.Equal(traceId, message.MessageAttributes[TraceHeader].StringValue);
    }

    [Fact]
    public async Task Put_WhenNotFound_ShouldBeNotFound()
    {
        using var client = CreateHttpClient();

        var response = await client.PutAsJsonAsync(
            $"/iuunotification/{NewReference()}",
            new { iuuCheckOutcome = "IUUOK" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenExists_ShouldRemoveFromMongo()
    {
        var reference = NewReference();
        using var client = CreateHttpClient();
        await Create(client, reference, "IUUOK");

        var response = await client.DeleteAsync($"/iuunotification/{reference}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await FindIuuNotification(reference));
    }

    [Fact]
    public async Task Delete_WhenNotFound_ShouldBeNotFound()
    {
        using var client = CreateHttpClient();

        var response = await client.DeleteAsync(
            $"/iuunotification/{NewReference()}",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByReference_WhenExists_ShouldReturnStoredNotification()
    {
        var reference = NewReference();
        using var client = CreateHttpClient();
        await Create(client, reference, "IUUOK");
        var update = await client.PutAsJsonAsync(
            $"/iuunotification/{reference}",
            new { iuuCheckOutcome = "IUUNA" },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var notification = await client.GetFromJsonAsync<IuuNotificationMessage>(
            $"/iuunotification/{reference}",
            TestContext.Current.CancellationToken
        );

        var document = await FindIuuNotification(reference);
        Assert.NotNull(notification);
        Assert.Equal(reference, notification.Reference);
        Assert.Equal("IUUNA", notification.IuuCheckOutcome);
        Assert.Equal(document!["iuuNotification"]["lastUpdated"].ToUniversalTime(), notification.LastUpdated);
    }

    [Fact]
    public async Task GetByReference_WhenNotFound_ShouldBeNotFound()
    {
        using var client = CreateHttpClient();

        var response = await client.GetAsync(
            $"/iuunotification/{NewReference()}",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithNoQuery_ShouldReturnLatestTenNewestFirst()
    {
        using var client = CreateHttpClient();
        await Reset(client);
        var references = Enumerable.Range(1, 12).Select(i => $"IUU.GB.2026.{i:D7}").ToList();
        foreach (var reference in references)
        {
            await Create(client, reference, "IUUOK");
        }

        var notifications = await GetNotifications(client, "/iuunotification");

        Assert.Equal(references.AsEnumerable().Reverse().Take(10), notifications.Select(n => n.Reference));
    }

    [Fact]
    public async Task Get_WithQuery_ShouldSkipLimitAndSortAscending()
    {
        using var client = CreateHttpClient();
        await Reset(client);
        var references = Enumerable.Range(1, 5).Select(i => $"IUU.GB.2026.{i:D7}").ToList();
        foreach (var reference in references)
        {
            await Create(client, reference, "IUUOK");
        }

        var notifications = await GetNotifications(client, "/iuunotification?skip=1&limit=2&sortOrder=asc");

        Assert.Equal(references.Skip(1).Take(2), notifications.Select(n => n.Reference));
    }

    private static async Task Reset(HttpClient client)
    {
        var response = await client.PostAsync("/admin/reset", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<List<IuuNotificationMessage>> GetNotifications(HttpClient client, string url) =>
        await client.GetFromJsonAsync<List<IuuNotificationMessage>>(url, TestContext.Current.CancellationToken) ?? [];

    private static async Task Create(HttpClient client, string reference, string iuuCheckOutcome)
    {
        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{reference}",
            new { iuuCheckOutcome },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static IuuNotificationMessage Deserialize(Message message) =>
        JsonSerializer.Deserialize<IuuNotificationMessage>(message.Body, s_jsonOptions)
        ?? throw new InvalidOperationException("Message body was empty");

    /// <summary>
    /// The shape the Trade Imports Processor consumes, defined here so the tests check the JSON contract
    /// rather than reuse the API's own model.
    /// </summary>
    private sealed record IuuNotificationMessage(string Reference, DateTime LastUpdated, string IuuCheckOutcome);
}
