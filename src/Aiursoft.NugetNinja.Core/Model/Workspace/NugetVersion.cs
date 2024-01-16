namespace Aiursoft.NugetNinja.Core.Model.Workspace;

public sealed class NugetVersion : ICloneable, IComparable<NugetVersion?>, IEquatable<NugetVersion?>
{
    public NugetVersion(string versionString)
    {
        SourceString = versionString;
        var trimmedVersion = versionString.Replace("*", "0");
        if (trimmedVersion.Contains("-"))
        {
            PrimaryVersion = Version.Parse(trimmedVersion.Split("-")[0]);
            AdditionalText = trimmedVersion.Split("-")[1].ToLower().Trim();
        }
        else
        {
            PrimaryVersion = Version.Parse(trimmedVersion);
        }
    }

    public string SourceString { get; }
    public Version PrimaryVersion { get; }
    public string AdditionalText { get; } = string.Empty;

    public object Clone()
    {
        return new NugetVersion(SourceString);
    }

    public int CompareTo(NugetVersion? otherNugetVersion)
    {
        if (ReferenceEquals(otherNugetVersion, null)) throw new ArgumentNullException(nameof(otherNugetVersion));

        if (!PrimaryVersion.Equals(otherNugetVersion.PrimaryVersion))
            return PrimaryVersion.CompareTo(otherNugetVersion.PrimaryVersion);

        if (!string.IsNullOrWhiteSpace(AdditionalText) &&
            !string.IsNullOrWhiteSpace(otherNugetVersion.AdditionalText))
            return string.CompareOrdinal(AdditionalText, otherNugetVersion.AdditionalText);
        if (!string.IsNullOrWhiteSpace(AdditionalText)) return -1;
        if (!string.IsNullOrWhiteSpace(otherNugetVersion.AdditionalText)) return 1;
        return 0;
    }

    public bool Equals(NugetVersion? otherNugetVersion)
    {
        if (ReferenceEquals(otherNugetVersion, null)) return false;
        return
            PrimaryVersion.Equals(otherNugetVersion.PrimaryVersion) &&
            AdditionalText.Equals(otherNugetVersion.AdditionalText);
    }

    public static bool operator ==(NugetVersion? lvs, NugetVersion? rvs)
    {
        return lvs?.Equals(rvs) ?? ReferenceEquals(lvs, rvs);
    }

    public static bool operator !=(NugetVersion? lvs, NugetVersion? rvs)
    {
        return !(lvs == rvs);
    }

    public static bool operator <(NugetVersion? lvs, NugetVersion? rvs)
    {
        return lvs?.CompareTo(rvs) < 0;
    }

    public static bool operator >(NugetVersion? lvs, NugetVersion? rvs)
    {
        return lvs?.CompareTo(rvs) > 0;
    }

    public bool IsPreviewVersion()
    {
        return !string.IsNullOrWhiteSpace(AdditionalText);
    }

    public override string ToString()
    {
        return $"{PrimaryVersion}-{AdditionalText}".TrimEnd('-');
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;
        if (ReferenceEquals(this, null))
            return false;
        if (obj is NugetVersion nuVersion)
            return Equals(nuVersion);
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(PrimaryVersion, AdditionalText);
    }
}