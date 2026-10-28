using System.ComponentModel.DataAnnotations;

namespace TradeImportsFisSimulatorApi.Utils.Logging;

public class TraceHeader
{
    [ConfigurationKeyName("TraceHeader")]
    [Required]
    public required string Name { get; set; }
}
