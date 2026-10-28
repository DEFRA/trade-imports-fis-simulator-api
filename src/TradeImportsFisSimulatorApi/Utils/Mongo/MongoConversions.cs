using MongoDB.Bson.Serialization.Conventions;

namespace TradeImportsFisSimulatorApi.Utils.Mongo;

public static class MongoConventions
{
    private static int s_initialized;

    public static void Register()
    {
        if (Interlocked.Exchange(ref s_initialized, 1) == 1)
        {
            return;
        }

        // Extra elements are ignored so documents stored in an older shape can still be read.
        var conversions = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
        };

        ConventionRegistry.Register("CamelCase", conversions, _ => true);
    }
}
