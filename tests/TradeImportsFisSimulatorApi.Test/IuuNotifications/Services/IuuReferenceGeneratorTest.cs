using TradeImportsFisSimulatorApi.IuuNotifications.Services;

namespace TradeImportsFisSimulatorApi.Test.IuuNotifications.Services;

public class IuuReferenceGeneratorTest
{
    [Fact]
    public void Next_WhenNoReferences_ShouldReturnOne()
    {
        Assert.Equal("1", IuuReferenceGenerator.Next([]));
    }

    [Fact]
    public void Next_ShouldIncrementLargestNumberKeepingPrefixAndPadding()
    {
        var next = IuuReferenceGenerator.Next(["IUU.GB.2026.0000009", "IUU.GB.2026.0000041", "IUU.GB.2026.0000010"]);

        Assert.Equal("IUU.GB.2026.0000042", next);
    }

    [Fact]
    public void Next_WhenNumberOverflowsPadding_ShouldGrow()
    {
        Assert.Equal("IUU.1000", IuuReferenceGenerator.Next(["IUU.999"]));
    }

    [Fact]
    public void Next_ShouldIgnoreReferencesWithoutTrailingNumber()
    {
        Assert.Equal("6", IuuReferenceGenerator.Next(["abc", "5"]));
    }
}
