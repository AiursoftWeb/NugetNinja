using System.Text.RegularExpressions;

namespace Aiursoft.NugetNinja.Core;
public class VersionCrossChecker
{
    public static readonly string[] netVersions = new string[]
    {
        "netcoreapp1.0", 
        "netcoreapp1.1", 
        "netcoreapp2.0", 
        "netcoreapp2.1", 
        "netcoreapp2.2",
        "netcoreapp3.0", 
        "netcoreapp3.1", 
        "netframework4.8",
        "netframework4.7",
        "netframework4.6",
        "netframework4.5",
        "net5.0", 
        "net6.0", 
        "net7.0",
        "net8.0",
        "net9.0",
    };

    public bool LikeRuntimeVersions(IEnumerable<NugetVersion> inputList)
    {
        var dotnetVersions = GetDotNetVersionsNumbers();
        return inputList
            .Select(inputVersion => new Version(inputVersion.PrimaryVersion.Major, inputVersion.PrimaryVersion.Minor))
            .All(firstTwoVersion => dotnetVersions.Contains(firstTwoVersion));
    }

    private List<Version> GetDotNetVersionsNumbers()
    {
        var versionsList = new List<Version>();
        var regex = new Regex(@"\d([.\d]+)?");

        foreach (string version in netVersions)
        {
            var matches = regex.Matches(version);

            if (matches.Count <= 0) continue;
            var matchedVersion = matches[0].Value;
            if (Version.TryParse(matchedVersion, out var parsedVersion))
            {
                versionsList.Add(parsedVersion);
            }
        }

        return versionsList;
    }
}
