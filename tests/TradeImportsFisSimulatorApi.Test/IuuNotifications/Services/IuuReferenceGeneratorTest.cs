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

    [Theory]
    [InlineData("IUU.GB.2026.0000041", true, 41)]
    [InlineData("7", true, 7)]
    [InlineData("IUU.TEST.abc", false, 0)]
    [InlineData("IUU.99999999999999999999", false, 0)]
    public void TryGetTrailingNumber_ShouldParseTrailingNumberThatFitsInLong(
        string reference,
        bool expected,
        long expectedNumber
    )
    {
        var result = IuuReferenceGenerator.TryGetTrailingNumber(reference, out var number);

        Assert.Equal(expected, result);
        Assert.Equal(expectedNumber, number);
    }
}
