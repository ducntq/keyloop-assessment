using System.Text.RegularExpressions;
using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Domain.ValueObjects;

/// <summary>
/// Vehicle Identification Number value object.
/// Validates the standard 17-character format: uppercase alphanumerics that
/// exclude the ambiguous letters I, O and Q.
/// </summary>
public readonly record struct Vin
{
    private static readonly Regex StandardVinPattern =
        new("^[A-HJ-NPR-Z0-9]{17}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const int StandardLength = 17;

    public string Value { get; }

    public Vin(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("VIN must not be null or empty.");
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length != StandardLength || !StandardVinPattern.IsMatch(normalized))
        {
            throw new DomainValidationException(
                $"VIN must be exactly {StandardLength} characters using digits and letters " +
                "A-Z excluding I, O and Q.");
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}
