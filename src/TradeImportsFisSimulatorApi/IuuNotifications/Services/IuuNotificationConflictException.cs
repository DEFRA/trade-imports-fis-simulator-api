namespace TradeImportsFisSimulatorApi.IuuNotifications.Services;

public sealed class IuuNotificationConflictException(string reference, Exception innerException)
    : Exception($"An IUU notification with reference '{reference}' already exists.", innerException);
