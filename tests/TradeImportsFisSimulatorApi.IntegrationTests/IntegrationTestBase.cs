using MongoDB.Bson;
using MongoDB.Driver;

namespace TradeImportsFisSimulatorApi.IntegrationTests;

/// <summary>
/// Integration tests run against the stack started by compose.yml (API, MongoDB and floci).
/// They share state, so they run in a single non-parallel collection.
/// </summary>
[Trait("Category", "IntegrationTest")]
[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase
{
    protected const string TraceHeader = "x-cdp-request-id";

    private const int ApiPort = 8085;
    private const string MongoUri = "mongodb://127.0.0.1:27017/?directConnection=true";
    private const string DatabaseName = "trade-imports-fis-simulator-api";
    private const string IuuNotificationsCollection = "iuunotifications";

    protected static HttpClient CreateHttpClient() => new() { BaseAddress = new Uri($"http://localhost:{ApiPort}") };

    protected static IMongoCollection<BsonDocument> GetIuuNotificationsCollection()
    {
        var settings = MongoClientSettings.FromConnectionString(MongoUri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.SocketTimeout = TimeSpan.FromSeconds(5);

        return new MongoClient(settings)
            .GetDatabase(DatabaseName)
            .GetCollection<BsonDocument>(IuuNotificationsCollection);
    }

    protected static Task<BsonDocument?> FindIuuNotification(string reference) =>
        GetIuuNotificationsCollection().Find(Builders<BsonDocument>.Filter.Eq("_id", reference)).FirstOrDefaultAsync()!;

    // Ends in a letter so it has no trailing number and is not recorded by the next id counter.
    protected static string NewReference() => $"IUU.TEST.{Guid.NewGuid():N}.x";
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection
{
    public const string Name = "Integration Tests";
}
