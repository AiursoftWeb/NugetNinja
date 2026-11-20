using System.Globalization;

namespace Aiursoft.NugetNinja.Core.Services.Extractor;

public class NinjaProposalCooldowns
{
    public string Nuget { get; set; } = "0";

    public string Npm { get; set; } = "0";

    public string Translation { get; set; } = "0";

    public TimeSpan NugetInterval => Parse(Nuget, "nuget");

    public TimeSpan NpmInterval => Parse(Npm, "npm");

    public TimeSpan TranslationInterval => Parse(Translation, "translation");

    internal void Validate()
    {
        _ = NugetInterval;
        _ = NpmInterval;
        _ = TranslationInterval;
    }

    private static TimeSpan Parse(string? value, string field)
    {
        if (value == "0")
        {
            return TimeSpan.Zero;
        }

        if (value is { Length: > 1 } &&
            int.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            if (value[^1] == 'h')
            {
                return TimeSpan.FromHours(count);
            }

            if (value[^1] == 'd' && count <= TimeSpan.MaxValue.Days)
            {
                return TimeSpan.FromDays(count);
            }
        }

        throw new InvalidDataException($"proposalCooldowns.{field} must be 0 or a whole number of hours (h) or days (d).");
    }
}
