using System.Diagnostics.CodeAnalysis;
using MongoDB.Driver;
using TradeImportsFisSimulatorApi.Utils.Mongo;

namespace TradeImportsFisSimulatorApi.IuuNotifications.Services;

public interface IIuuReferenceCounter
{
    /// <summary>
    /// Records a created reference, if its trailing number is larger than the largest recorded so far.
    /// </summary>
    Task RecordAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the reference with the largest trailing number recorded so far, or null if none has been recorded.
    /// </summary>
    Task<string?> GetLastReferenceAsync(CancellationToken cancellationToken = default);
}

public class IuuReferenceCounterDocument
{
    public required string Id { get; init; }

    public required string LastReference { get; init; }

    public long LastNumber { get; init; }
}

/// <summary>
/// Keeps the largest IUU reference in the counters collection, so the next id carries on from it
/// after the notifications have been deleted. /admin/reset does not clear this collection.
/// </summary>
[ExcludeFromCodeCoverage]
public class IuuReferenceCounter(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<IuuReferenceCounterDocument>(connectionFactory, CollectionName, loggerFactory),
        IIuuReferenceCounter
{
    public const string CollectionName = "counters";

    private const string CounterId = "iuuNotification";

    public async Task RecordAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (!IuuReferenceGenerator.TryGetTrailingNumber(reference, out var number))
        {
            return;
        }

        var update = Builders<IuuReferenceCounterDocument>
            .Update.Set(c => c.LastReference, reference)
            .Set(c => c.LastNumber, number);

        try
        {
            // Only matches when the new number is larger. If the counter exists with a larger or equal
            // number the upsert tries to insert a second document with the same _id, which fails.
            await Collection.UpdateOneAsync(
                c => c.Id == CounterId && c.LastNumber < number,
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken
            );
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // The recorded reference is already larger.
        }
    }

    public async Task<string?> GetLastReferenceAsync(CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(c => c.Id == CounterId)
            .Project(c => c.LastReference)
            .FirstOrDefaultAsync(cancellationToken);
    }

    protected override List<CreateIndexModel<IuuReferenceCounterDocument>> DefineIndexes(
        IndexKeysDefinitionBuilder<IuuReferenceCounterDocument> builder
    )
    {
        return [];
    }
}
