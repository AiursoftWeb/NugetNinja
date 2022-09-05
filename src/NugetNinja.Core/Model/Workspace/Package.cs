﻿

namespace Aiursoft.NugetNinja.Core;

public class Package
{
    public Package(string name, string versionText)
    {
        Name = name;
        SourceVersionText = versionText;

        if (versionText.Contains('(') ||
            versionText.Contains(')') ||
            versionText.Contains('[') ||
            versionText.Contains(']'))
        {
            versionText = versionText
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("[", string.Empty)
                .Replace("]", string.Empty)
                .Split(',')[0];
        }

        Version = new NugetVersion(versionText);
    }

    public Package(string name, NugetVersion version)
    {
        Name = name;
        Version = version;
        SourceVersionText = version.ToString();
    }

    public string Name { get; set; }

    public NugetVersion Version { get; set; }
    public string SourceVersionText { get; set; }

    public override string ToString() => Name;
}
