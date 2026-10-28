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

    /// <summary>
    /// Gets the trailing number of a reference, e.g. 41 for "IUU.GB.0000041". Returns false if the reference
    /// does not end in a number, or the number does not fit in a long.
    /// </summary>
    public static bool TryGetTrailingNumber(string reference, out long number)
    {
        number = 0;
        var match = TrailingNumber().Match(reference);
        return match.Success && long.TryParse(match.Groups["number"].Value, out number);
    }

    [GeneratedRegex(@"^(?<prefix>.*?)(?<number>\d+)$")]
    private static partial Regex TrailingNumber();
}
