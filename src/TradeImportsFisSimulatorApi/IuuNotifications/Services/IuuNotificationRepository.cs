using System.Diagnostics.CodeAnalysis;
using MongoDB.Driver;
using TradeImportsFisSimulatorApi.IuuNotifications.Models;
using TradeImportsFisSimulatorApi.Utils.Auditing;
using TradeImportsFisSimulatorApi.Utils.Mongo;

namespace TradeImportsFisSimulatorApi.IuuNotifications.Services;

public interface IIuuNotificationRepository
{
    Task<IuuNotificationDocument?> GetAsync(string reference, CancellationToken cancellationToken = default);

    Task CreateAsync(IuuNotificationDocument notification, CancellationToken cancellationToken = default);

    Task<IuuNotificationDocument?> UpdateAsync(
        string reference,
        string? iuuCheckOutcome,
        DateTime lastUpdated,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteAsync(string reference, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IuuNotificationDocument>> GetByCreatedAsync(
        int skip,
        int limit,
        SortOrder sortOrder,
        CancellationToken cancellationToken = default
    );
}

[ExcludeFromCodeCoverage]
public class IuuNotificationRepository(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<IuuNotificationDocument>(connectionFactory, "iuunotifications", loggerFactory),
        IIuuNotificationRepository
{
    public async Task<IuuNotificationDocument?> GetAsync(
        string reference,
        CancellationToken cancellationToken = default
    )
    {
        return await Collection.Find(e => e.Id == reference).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateAsync(IuuNotificationDocument notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await Collection.InsertOneAsync(notification, cancellationToken: cancellationToken);
            Logger.Audit("Created IUU notification: {Reference}", notification.Id);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new IuuNotificationConflictException(notification.Id, ex);
        }
    }

    public async Task<IuuNotificationDocument?> UpdateAsync(
        string reference,
        string? iuuCheckOutcome,
        DateTime lastUpdated,
        CancellationToken cancellationToken = default
    )
    {
        var update = Builders<IuuNotificationDocument>
            .Update.Set(e => e.IuuNotification.IuuCheckOutcome, iuuCheckOutcome)
            .Set(e => e.IuuNotification.LastUpdated, lastUpdated);

        var result = await Collection.FindOneAndUpdateAsync(
            e => e.Id == reference,
            update,
            new FindOneAndUpdateOptions<IuuNotificationDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken
        );

        if (result is not null)
        {
            Logger.Audit("Updated IUU notification: {Reference}", reference);
        }

        return result;
    }

    public async Task<bool> DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        var result = await Collection.DeleteOneAsync(e => e.Id == reference, cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task<IReadOnlyCollection<IuuNotificationDocument>> GetByCreatedAsync(
        int skip,
        int limit,
        SortOrder sortOrder,
        CancellationToken cancellationToken = default
    )
    {
        // The reference breaks ties, as notifications created close together can share a timestamp.
        var sort =
            sortOrder == SortOrder.Asc
                ? Builders<IuuNotificationDocument>.Sort.Ascending(e => e.Created).Ascending(e => e.Id)
                : Builders<IuuNotificationDocument>.Sort.Descending(e => e.Created).Descending(e => e.Id);

        return await Collection.Find(_ => true).Sort(sort).Skip(skip).Limit(limit).ToListAsync(cancellationToken);
    }

    protected override List<CreateIndexModel<IuuNotificationDocument>> DefineIndexes(
        IndexKeysDefinitionBuilder<IuuNotificationDocument> builder
    )
    {
        // The reference is stored as _id, which is already uniquely indexed.
        return [];
    }
}
