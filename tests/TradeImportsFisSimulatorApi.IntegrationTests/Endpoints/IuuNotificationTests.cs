using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.SQS.Model;
using Defra.TradeImportsDataApi.Domain.Events;

namespace TradeImportsFisSimulatorApi.IntegrationTests.Endpoints;

public class IuuNotificationTests : SqsTestBase
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Post_WhenNew_ShouldPersistAndPublishCreatedEvent()
    {
        var reference = NewReference();
        var traceId = Guid.NewGuid().ToString();
        using var client = CreateHttpClient();
        client.DefaultRequestHeaders.Add(TraceHeader, traceId);

        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{reference}",
            new { iuuOption = "IUUOK" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var document = await FindIuuNotification(reference);
        Assert.NotNull(document);
        Assert.Equal("IUUOK", document["iuuNotification"]["iuuOption"].AsString);

        var message = await WaitForMessage(reference, ResourceEventOperations.Created);
        var resourceEvent = Deserialize(message);
        Assert.Equal(ResourceEventResourceTypes.IuuNotification, resourceEvent.ResourceType);
        Assert.Equal(reference, resourceEvent.Resource!.IuuNotification.Reference);
        Assert.Equal("IUUOK", resourceEvent.Resource.IuuNotification.IuuOption);
        Assert.Equal(document["etag"].AsString, resourceEvent.Etag);

        Assert.Equal(ResourceEventResourceTypes.IuuNotification, message.MessageAttributes["ResourceType"].StringValue);
        Assert.StartsWith(
            "Defra.TradeImportsDataApi.Domain.Events.ResourceEvent`1",
            message.MessageAttributes["MessageType"].StringValue
        );
        Assert.Equal(traceId, message.MessageAttributes[TraceHeader].StringValue);
    }

    [Fact]
    public async Task Post_WhenAlreadyExists_ShouldBeConflict()
    {
        var reference = NewReference();
        using var client = CreateHttpClient();
        await Create(client, reference, "IUUOK");

        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{reference}",
            new { iuuOption = "IUUNA" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var document = await FindIuuNotification(reference);
        Assert.Equal("IUUOK", document!["iuuNotification"]["iuuOption"].AsString);
    }

    [Fact]
    public async Task Put_WhenExists_ShouldPersistAndPublishUpdatedEvent()
    {
        var reference = NewReference();
        var traceId = Guid.NewGuid().ToString();
        using var client = CreateHttpClient();
        await Create(client, reference, "IUUOK");
        var created = await FindIuuNotification(reference);

        client.DefaultRequestHeaders.Add(TraceHeader, traceId);
        var response = await client.PutAsJsonAsync(
            $"/iuunotification/{reference}",
            new { iuuOption = "IUUNotCompliant" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await FindIuuNotification(reference);
        Assert.NotNull(updated);
        Assert.Equal("IUUNotCompliant", updated["iuuNotification"]["iuuOption"].AsString);
        Assert.NotEqual(created!["etag"].AsString, updated["etag"].AsString);
        Assert.Equal(created["created"].ToUniversalTime(), updated["created"].ToUniversalTime());

        var message = await WaitForMessage(reference, ResourceEventOperations.Updated);
        var resourceEvent = Deserialize(message);
        Assert.Equal("IUUNotCompliant", resourceEvent.Resource!.IuuNotification.IuuOption);
        Assert.Equal(updated["etag"].AsString, resourceEvent.Etag);
        Assert.Equal(traceId, message.MessageAttributes[TraceHeader].StringValue);
    }

    [Fact]
    public async Task Put_WhenNotFound_ShouldBeNotFound()
    {
        using var client = CreateHttpClient();

        var response = await client.PutAsJsonAsync(
            $"/iuunotification/{NewReference()}",
            new { iuuOption = "IUUOK" },
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

        Assert.Equal(references.AsEnumerable().Reverse().Take(10), notifications.Select(n => n.Id));
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

        Assert.Equal(references.Skip(1).Take(2), notifications.Select(n => n.Id));
    }

    private static async Task Reset(HttpClient client)
    {
        var response = await client.PostAsync("/admin/reset", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<List<IuuNotificationEvent>> GetNotifications(HttpClient client, string url) =>
        await client.GetFromJsonAsync<List<IuuNotificationEvent>>(url, TestContext.Current.CancellationToken) ?? [];

    private static async Task Create(HttpClient client, string reference, string iuuOption)
    {
        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{reference}",
            new { iuuOption },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static ResourceEvent<IuuNotificationEvent> Deserialize(Message message) =>
        JsonSerializer.Deserialize<ResourceEvent<IuuNotificationEvent>>(message.Body, s_jsonOptions)
        ?? throw new InvalidOperationException("Message body was empty");
}
