using System.Globalization;
using System.Text.RegularExpressions;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;

public sealed record NpmVersion(int Major, int Minor, int Patch) : IComparable<NpmVersion>
{
    public static NpmVersion Parse(string value)
    {
        var match = Regex.Match(value, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new NotSupportedException($"npm updates require a stable three-part version, found '{value}'.");
        }

        return new NpmVersion(
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
    }

    public bool Allows(NpmVersion candidate) =>
        candidate.CompareTo(this) >= 0 && candidate.Major == Major && (Major != 0 || candidate.Minor == Minor);

    public string UpdateRange => Major == 0
        ? $">={this} <0.{checked(Minor + 1)}.0"
        : $">={this} <{checked(Major + 1)}.0.0";

    public NpmVersion NextPatch() => this with { Patch = checked(Patch + 1) };

    public int CompareTo(NpmVersion? other)
    {
        if (other == null) return 1;
        var major = Major.CompareTo(other.Major);
        if (major != 0) return major;
        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
