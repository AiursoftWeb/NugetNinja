namespace Aiursoft.NugetNinja.MissingPropertyPlugin.Services;

public static class LicenseExpressionParser
{
    /// <summary>
    /// Represents a collection of license patterns used to detect the type of license based on the content.
    /// </summary>
    private static readonly Dictionary<string, string[]> LicensePatterns = new()
    {
        { "MIT", new[] { "MIT License" } },
        { "Apache-2.0", new[] { "Apache License", "Version 2.0" } },
        { "GPL-3.0", new[] { "GNU GENERAL PUBLIC LICENSE", "Version 3" } },
        { "GPL-2.0", new[] { "GNU GENERAL PUBLIC LICENSE", "Version 2" } },
        { "BSD-3-Clause", new[] { "BSD 3-Clause License" } },
        { "BSD-2-Clause", new[] { "BSD 2-Clause License" } },
        { "MPL-2.0", new[] { "Mozilla Public License", "Version 2.0" } },
        { "LGPL-3.0", new[] { "GNU LESSER GENERAL PUBLIC LICENSE", "Version 3" } },
        { "LGPL-2.1", new[] { "GNU LESSER GENERAL PUBLIC LICENSE", "Version 2.1" } },
        { "AGPL-3.0", new[] { "GNU AFFERO GENERAL PUBLIC LICENSE", "Version 3" } },
        { "EPL-2.0", new[] { "Eclipse Public License", "Version 2.0" } },
        { "Unlicense", new[] { "This is free and unencumbered software", "The Unlicense" } },
        { "CC-BY-4.0", new[] { "Creative Commons Attribution", "Version 4.0" } },
        { "CC-BY-SA-4.0", new[] { "Creative Commons Attribution-ShareAlike", "Version 4.0" } },
        { "CC0-1.0", new[] { "Creative Commons Zero", "Version 1.0" } },
        { "ISC", new[] { "ISC License" } },
        { "Zlib", new[] { "zlib License" } },
        { "OpenSSL", new[] { "OpenSSL License" } },
        { "Artistic-2.0", new[] { "Artistic License", "Version 2.0" } },
        { "EUPL-1.2", new[] { "European Union Public License", "Version 1.2" } }
        // Additional licenses can be added here
    };

    public static string Parse(string licenseContent)
    {
        var license = licenseContent.Trim();
        var lines = license.Split('\n').Select(line => line.Trim()).ToArray();

        // Scan first few lines to find the license type
        foreach (var line in lines.Take(10)) // Only scan the first 10 lines for performance
        {
            foreach (var licenseType in LicensePatterns.Where(licenseType => licenseType.Value.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
            {
                return licenseType.Key;
            }
        }

        // In case the first few lines don't contain the license type, scan the whole content
        foreach (var licenseType in LicensePatterns.Where(licenseType => licenseType.Value.Any(keyword => licenseContent.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
        {
            return licenseType.Key;
        }

        // If no license type is found, return an empty string
        return string.Empty;
    }
}