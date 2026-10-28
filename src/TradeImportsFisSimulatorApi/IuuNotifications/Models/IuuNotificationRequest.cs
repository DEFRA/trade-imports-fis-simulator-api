namespace TradeImportsFisSimulatorApi.IuuNotifications.Models;

public sealed class IuuNotificationRequest
{
    // Must be sent, so a misnamed field is refused (400), but may be null: a notification with no outcome yet.
    public required string? IuuCheckOutcome { get; init; }
}
