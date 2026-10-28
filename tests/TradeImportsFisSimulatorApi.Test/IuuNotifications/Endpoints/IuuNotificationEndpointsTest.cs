using System.Net;
using System.Net.Http.Json;
using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Iuu;
using MongoDB.Driver;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TradeImportsFisSimulatorApi.IuuNotifications.Models;
using TradeImportsFisSimulatorApi.IuuNotifications.Services;

namespace TradeImportsFisSimulatorApi.Test.IuuNotifications.Endpoints;

public class IuuNotificationEndpointsTest
{
    private const string Reference = "IUU.GB.2026.0000001";

    [Fact]
    public async Task Post_WhenNew_ShouldPersistPublishAndReturnCreated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{Reference}",
            new { iuuOption = "IUUOK" },
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/iuunotification/{Reference}", response.Headers.Location?.OriginalString);

        var body = await response.Content.ReadFromJsonAsync<ResourceEvent<IuuNotificationEvent>>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(Reference, body.ResourceId);
        Assert.Equal(ResourceEventResourceTypes.IuuNotification, body.ResourceType);
        Assert.Equal(ResourceEventOperations.Created, body.Operation);
        Assert.Equal("IUUOK", body.Resource!.IuuNotification.IuuOption);
        Assert.Equal(body.Resource.Etag, body.Etag);

        await factory
            .Repository.Received(1)
            .CreateAsync(
                Arg.Is<IuuNotificationEvent>(n =>
                    n.Id == Reference
                    && n.IuuNotification.Reference == Reference
                    && n.IuuNotification.IuuOption == "IUUOK"
                    && n.Created == n.Updated
                    && !string.IsNullOrEmpty(n.Etag)
                ),
                Arg.Any<CancellationToken>()
            );
        await factory
            .Publisher.Received(1)
            .PublishAsync(
                Arg.Is<ResourceEvent<IuuNotificationEvent>>(e =>
                    e.ResourceId == Reference && e.Operation == ResourceEventOperations.Created
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Post_WhenAlreadyExists_ShouldReturnConflictAndNotPublish()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory
            .Repository.CreateAsync(Arg.Any<IuuNotificationEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IuuNotificationConflictException(Reference, new InvalidOperationException()));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{Reference}",
            new { iuuOption = "IUUOK" },
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await factory.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_WhenExists_ShouldPublishUpdatedEventAndReturnOk()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        var updated = CreateNotification("IUUNotCompliant");
        factory
            .Repository.UpdateAsync(
                Reference,
                "IUUNotCompliant",
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(updated);
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/iuunotification/{Reference}",
            new { iuuOption = "IUUNotCompliant" },
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ResourceEvent<IuuNotificationEvent>>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(ResourceEventOperations.Updated, body.Operation);
        Assert.Equal(updated.Etag, body.Etag);
        Assert.Equal(updated.Updated, body.Timestamp);
        Assert.Equal("IUUNotCompliant", body.Resource!.IuuNotification.IuuOption);

        await factory
            .Publisher.Received(1)
            .PublishAsync(
                Arg.Is<ResourceEvent<IuuNotificationEvent>>(e =>
                    e.ResourceId == Reference && e.Operation == ResourceEventOperations.Updated
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Put_WhenNotFound_ShouldReturnNotFoundAndNotPublish()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory
            .Repository.UpdateAsync(
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((IuuNotificationEvent?)null);
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/iuunotification/{Reference}",
            new { iuuOption = "IUUOK" },
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await factory.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, HttpStatusCode.NoContent)]
    [InlineData(false, HttpStatusCode.NotFound)]
    public async Task Delete_ShouldReturnStatusFromRepository(bool deleted, HttpStatusCode expected)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory.Repository.DeleteAsync(Reference, Arg.Any<CancellationToken>()).Returns(deleted);
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/iuunotification/{Reference}", cancellationToken);

        Assert.Equal(expected, response.StatusCode);
        await factory.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_WhenMongoFails_ShouldReturnServerErrorAndNotPublish()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory
            .Repository.CreateAsync(Arg.Any<IuuNotificationEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MongoException("unavailable"));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{Reference}",
            new { iuuOption = "IUUOK" },
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await factory.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_WithNoQuery_ShouldUseDefaultsAndReturnNotifications()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory
            .Repository.GetByCreatedAsync(0, 10, SortOrder.Desc, Arg.Any<CancellationToken>())
            .Returns([CreateNotification("IUUOK")]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/iuunotification", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<IuuNotificationEvent>>(cancellationToken);
        var notification = Assert.Single(body!);
        Assert.Equal(Reference, notification.Id);
        Assert.Equal("IUUOK", notification.IuuNotification.IuuOption);
    }

    [Theory]
    [InlineData("asc", SortOrder.Asc)]
    [InlineData("Asc", SortOrder.Asc)]
    [InlineData("desc", SortOrder.Desc)]
    [InlineData("Desc", SortOrder.Desc)]
    public async Task Get_WithQuery_ShouldPassSkipLimitAndSortOrderToRepository(string sortOrder, SortOrder expected)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory
            .Repository.GetByCreatedAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<SortOrder>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/iuunotification?skip=5&limit=20&sortOrder={sortOrder}",
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await factory.Repository.Received(1).GetByCreatedAsync(5, 20, expected, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("skip=-1")]
    [InlineData("limit=0")]
    [InlineData("sortOrder=sideways")]
    public async Task Get_WithInvalidQuery_ShouldReturnBadRequest(string query)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/iuunotification?{query}", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory
            .Repository.DidNotReceiveWithAnyArgs()
            .GetByCreatedAsync(default, default, default, Arg.Any<CancellationToken>());
    }

    private static IuuNotificationEvent CreateNotification(string iuuOption)
    {
        var created = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        var updated = created.AddHours(1);

        return new IuuNotificationEvent
        {
            Id = Reference,
            Etag = "etag-2",
            Created = created,
            Updated = updated,
            IuuNotification = new IuuNotification
            {
                Reference = Reference,
                LastUpdated = updated,
                IuuOption = iuuOption,
            },
        };
    }
}
