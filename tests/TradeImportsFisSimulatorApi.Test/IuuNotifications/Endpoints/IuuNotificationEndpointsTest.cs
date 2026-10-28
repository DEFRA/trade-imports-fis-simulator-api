using System.Net;
using System.Net.Http.Json;
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
            new { iuuCheckOutcome = "IUUOK" },
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/iuunotification/{Reference}", response.Headers.Location?.OriginalString);

        var body = await response.Content.ReadFromJsonAsync<IuuNotification>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(Reference, body.Reference);
        Assert.Equal("IUUOK", body.IuuCheckOutcome);
        Assert.Equal(0, body.LastUpdated.Ticks % TimeSpan.TicksPerMillisecond);

        await factory
            .Repository.Received(1)
            .CreateAsync(
                Arg.Is<IuuNotificationDocument>(d =>
                    d.Id == Reference
                    && d.IuuNotification.Reference == Reference
                    && d.IuuNotification.IuuCheckOutcome == "IUUOK"
                    && d.Created == d.IuuNotification.LastUpdated
                ),
                Arg.Any<CancellationToken>()
            );
        await factory.ReferenceCounter.Received(1).RecordAsync(Reference, Arg.Any<CancellationToken>());
        await factory
            .Publisher.Received(1)
            .PublishAsync(
                Arg.Is<IuuNotification>(n =>
                    n.Reference == Reference && n.IuuCheckOutcome == "IUUOK" && n.LastUpdated == body.LastUpdated
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"iuuCheckOutcome\":\"\"}")]
    [InlineData("{\"iuuOption\":\"IUUOK\"}")]
    public async Task Post_WhenIuuCheckOutcomeMissing_ShouldReturnBadRequestAndNotPersist(string json)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/iuunotification/{Reference}",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.Repository.DidNotReceiveWithAnyArgs().CreateAsync(default!, Arg.Any<CancellationToken>());
        await factory.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_WhenAlreadyExists_ShouldReturnConflictAndNotPublish()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory
            .Repository.CreateAsync(Arg.Any<IuuNotificationDocument>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IuuNotificationConflictException(Reference, new InvalidOperationException()));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{Reference}",
            new { iuuCheckOutcome = "IUUOK" },
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await factory.ReferenceCounter.DidNotReceiveWithAnyArgs().RecordAsync(default!, Arg.Any<CancellationToken>());
        await factory.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_WhenExists_ShouldPublishUpdatedNotificationAndReturnOk()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        var updated = CreateDocument("IUUNotCompliant");
        factory
            .Repository.UpdateAsync(Reference, "IUUNotCompliant", Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(updated);
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/iuunotification/{Reference}",
            new { iuuCheckOutcome = "IUUNotCompliant" },
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<IuuNotification>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal("IUUNotCompliant", body.IuuCheckOutcome);
        Assert.Equal(updated.IuuNotification.LastUpdated, body.LastUpdated);

        await factory.Publisher.Received(1).PublishAsync(updated.IuuNotification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_WhenNotFound_ShouldReturnNotFoundAndNotPublish()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory
            .Repository.UpdateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((IuuNotificationDocument?)null);
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/iuunotification/{Reference}",
            new { iuuCheckOutcome = "IUUOK" },
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await factory.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_WhenIuuCheckOutcomeMissing_ShouldReturnBadRequestAndNotUpdate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/iuunotification/{Reference}", new { }, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory
            .Repository.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default, Arg.Any<CancellationToken>());
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
            .Repository.CreateAsync(Arg.Any<IuuNotificationDocument>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MongoException("unavailable"));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/iuunotification/{Reference}",
            new { iuuCheckOutcome = "IUUOK" },
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await factory.Publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByReference_WhenExists_ShouldReturnNotification()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        var stored = CreateDocument("IUUNA");
        factory.Repository.GetAsync(Reference, Arg.Any<CancellationToken>()).Returns(stored);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/iuunotification/{Reference}", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IuuNotification>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(Reference, body.Reference);
        Assert.Equal(stored.IuuNotification.LastUpdated, body.LastUpdated);
        Assert.Equal("IUUNA", body.IuuCheckOutcome);
    }

    [Fact]
    public async Task GetByReference_WhenNotFound_ShouldReturnNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory.Repository.GetAsync(Reference, Arg.Any<CancellationToken>()).Returns((IuuNotificationDocument?)null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/iuunotification/{Reference}", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithNoQuery_ShouldUseDefaultsAndReturnNotifications()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new TestApplicationFactory();
        factory
            .Repository.GetByCreatedAsync(0, 10, SortOrder.Desc, Arg.Any<CancellationToken>())
            .Returns([CreateDocument("IUUOK")]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/iuunotification", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<IuuNotification>>(cancellationToken);
        var notification = Assert.Single(body!);
        Assert.Equal(Reference, notification.Reference);
        Assert.Equal("IUUOK", notification.IuuCheckOutcome);
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

    private static IuuNotificationDocument CreateDocument(string iuuCheckOutcome)
    {
        var created = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

        return new IuuNotificationDocument
        {
            Id = Reference,
            Created = created,
            IuuNotification = new IuuNotification
            {
                Reference = Reference,
                LastUpdated = created.AddHours(1),
                IuuCheckOutcome = iuuCheckOutcome,
            },
        };
    }
}
