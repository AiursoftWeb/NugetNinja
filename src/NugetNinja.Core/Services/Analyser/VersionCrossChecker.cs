using System.Text.RegularExpressions;

namespace Aiursoft.NugetNinja.Core;
public class VersionCrossChecker
{
    public static readonly string[] netVersions = new string[]
    {
        "netcoreapp1.0", "netcoreapp1.1", "netcoreapp3.0", "netcoreapp3.1", "net5.0", "net6.0", "net7.0"
    };

    public bool LikeRuntimeVersions(IEnumerable<NugetVersion> inputList)
    {
        var dotnetVersions = GetDotNetVersionsNumbers();
        foreach (var inputVersion in inputList)
        {
            var firstTwoVersion = new Version(inputVersion.PrimaryVersion.Major, inputVersion.PrimaryVersion.Minor);
            if (!dotnetVersions.Contains(firstTwoVersion))
            {
                return false;
            }
        }

        return true;
    }

    public List<Version> GetDotNetVersionsNumbers()
    {
        var versionsList = new List<Version>();
        var regex = new Regex(@"\d([.\d]+)?");

        foreach (string version in netVersions)
        {
            var matches = regex.Matches(version);

            if (matches.Count > 0)
            {
                var matchedVersion = matches[0].Value;
                if (Version.TryParse(matchedVersion, out var parsedVersion))
                {
                    versionsList.Add(parsedVersion);
                }
            }
        }

        return versionsList;
    }
}
