using System.Text;

namespace Aiursoft.NugetNinja.Core.Services.Utils;

public static class StringExtensions
{
    public static string GetAbsolutePath(string currentPath, string referencePath)
    {
        if (Path.IsPathRooted(referencePath)) return referencePath;

        referencePath = referencePath.Replace('\\', Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(currentPath, referencePath));
    }

    public static string PatToHeader(string patToken)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{patToken}"));
        return $"Basic {token}";
    }
}