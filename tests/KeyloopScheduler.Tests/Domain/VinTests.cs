using FluentAssertions;
using KeyloopScheduler.Domain.Exceptions;
using KeyloopScheduler.Domain.ValueObjects;

namespace KeyloopScheduler.Tests.Domain;

/// <summary>
/// Tier 1: VIN value-object format validation and normalization.
/// </summary>
[Trait("Category", "Domain")]
public sealed class VinTests
{
    [Fact]
    public void Accepts_a_standard_seventeen_character_vin()
    {
        var vin = new Vin("1HGBH41JXMN109186");

        vin.Value.Should().Be("1HGBH41JXMN109186");
        vin.ToString().Should().Be("1HGBH41JXMN109186");
    }

    [Fact]
    public void Normalizes_lowercase_and_whitespace()
    {
        new Vin("  1hgbh41jxmn109186  ").Value.Should().Be("1HGBH41JXMN109186");
    }

    [Theory]
    [InlineData("1HGBH41JXMN10918")]     // 16 characters
    [InlineData("1HGBH41JXMN1091866")]   // 18 characters
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_wrong_length_or_empty(string candidate)
    {
        var act = () => new Vin(candidate);

        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData("1HGBH41JXMN109I86")] // contains I
    [InlineData("1HGBH41JXMN109O86")] // contains O
    [InlineData("1HGBH41JXMN109Q86")] // contains Q
    [InlineData("1HGBH41JXMN109!86")] // non-alphanumeric
    public void Rejects_ambiguous_or_invalid_characters(string candidate)
    {
        var act = () => new Vin(candidate);

        act.Should().Throw<DomainValidationException>().WithMessage("*excluding I, O and Q*");
    }
}
