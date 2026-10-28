namespace TradeImportsFisSimulatorApi.IuuNotifications.Models;

/// <summary>
/// How an IUU notification is stored in MongoDB. The reference is the _id, and the created time is kept so
/// notifications can be listed in the order they were created.
/// </summary>
public class IuuNotificationDocument
{
    public required string Id { get; init; }

    public DateTime Created { get; init; }

    public required IuuNotification IuuNotification { get; init; }
}
