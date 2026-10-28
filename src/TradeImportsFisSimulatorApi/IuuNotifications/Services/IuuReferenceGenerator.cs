using System.Numerics;
using System.Text.RegularExpressions;

namespace TradeImportsFisSimulatorApi.IuuNotifications.Services;

public static partial class IuuReferenceGenerator
{
    private const string InitialReference = "1";

    /// <summary>
    /// Finds the reference with the largest trailing number and increments that number by one,
    /// keeping any prefix and zero padding, e.g. "IUU.GB.0000041" becomes "IUU.GB.0000042".
    /// </summary>
    public static string Next(IEnumerable<string> references)
    {
        var largest = references
            .Select(reference => TrailingNumber().Match(reference))
            .Where(match => match.Success)
            .Select(match => new
            {
                Prefix = match.Groups["prefix"].Value,
                Digits = match.Groups["number"].Value,
                Number = BigInteger.Parse(match.Groups["number"].Value),
            })
            .MaxBy(candidate => candidate.Number);

        if (largest is null)
        {
            return InitialReference;
        }

        var next = (largest.Number + 1).ToString().PadLeft(largest.Digits.Length, '0');
        return largest.Prefix + next;
    }

    [GeneratedRegex(@"^(?<prefix>.*?)(?<number>\d+)$")]
    private static partial Regex TrailingNumber();
}
