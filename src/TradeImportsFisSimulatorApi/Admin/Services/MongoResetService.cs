using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson;
using MongoDB.Driver;
using TradeImportsFisSimulatorApi.IuuNotifications.Services;
using TradeImportsFisSimulatorApi.Utils.Auditing;
using TradeImportsFisSimulatorApi.Utils.Mongo;

namespace TradeImportsFisSimulatorApi.Admin.Services;

public interface IMongoResetService
{
    Task ResetAsync(CancellationToken cancellationToken = default);
}

[ExcludeFromCodeCoverage]
public class MongoResetService(IMongoDbClientFactory connectionFactory, ILogger<MongoResetService> logger)
    : IMongoResetService
{
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        var database = connectionFactory.GetDatabase();
        var collectionNames = await (
            await database.ListCollectionNamesAsync(cancellationToken: cancellationToken)
        ).ToListAsync(cancellationToken);

        // Delete documents rather than dropping collections so that indexes are kept.
        // The counters are kept so that /admin/nextId does not start again from 1.
        foreach (
            var collectionName in collectionNames.Where(name =>
                !name.StartsWith("system.", StringComparison.Ordinal) && name != IuuReferenceCounter.CollectionName
            )
        )
        {
            var result = await database
                .GetCollection<BsonDocument>(collectionName)
                .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken);

            logger.Audit(
                "Reset collection {Collection}, deleted {Count} documents",
                collectionName,
                result.DeletedCount
            );
        }
    }
}
