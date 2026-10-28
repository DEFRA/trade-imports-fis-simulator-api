using System.Text.Json.Serialization;

namespace TradeImportsFisSimulatorApi.IuuNotifications.Models;

/// <summary>
/// The IUU notification message the Fish Import Service sends. Matches the model the Trade Imports Processor
/// consumes in Defra.TradeImportsProcessor.Processor.Models.Iuu.IuuNotification.
/// </summary>
public class IuuNotification
{
    // We don't know the format of the IUU message yet. The following have been inferred from the Data Dictionary

    [JsonPropertyName("reference")]
    public required string Reference { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    // Null is a notification with no outcome yet. It is still written to the message, as "iuuCheckOutcome": null.
    [JsonPropertyName("iuuCheckOutcome")]
    public string? IuuCheckOutcome { get; set; }
}
