namespace Aiursoft.NugetNinja.Core.Services.Extractor;

public class NinjaNugetConfig
{
    // ReSharper disable once CollectionNeverUpdated.Global
    public Dictionary<string, string[]> SameVersionGroups { get; set; } = [];

    internal void Validate()
    {
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (SameVersionGroups is null)
            throw new InvalidDataException("nuget.sameVersionGroups must be a mapping.");

        foreach (var (name, members) in SameVersionGroups)
        {
            if (string.IsNullOrWhiteSpace(name) || members is null || members.Length < 2)
                throw new InvalidDataException($"NuGet version group '{name}' must name at least two packages.");

            foreach (var member in members)
            {
                if (string.IsNullOrWhiteSpace(member) ||
                    member.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_')) ||
                    !assigned.Add(member))
                    throw new InvalidDataException($"Invalid or repeated NuGet package '{member}' in version group '{name}'.");
            }
        }
    }
}
