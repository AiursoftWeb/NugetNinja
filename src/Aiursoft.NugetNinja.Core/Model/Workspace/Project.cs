using Aiursoft.NugetNinja.Core.Services.IO;
using Aiursoft.NugetNinja.Core.Services.Utils;
using HtmlAgilityPack;

namespace Aiursoft.NugetNinja.Core.Model.Workspace;

public class Project(string pathOnDisk, HtmlNode projectDocument)
{
    // Global
    // Build and code
    // Tool
    // Best practice
    // Nuget
    public string PathOnDisk { get; set; } = pathOnDisk;
    public string FileName => Path.GetFileNameWithoutExtension(PathOnDisk);

    /// <summary>
    /// Usually can be one of the followings:
    ///
    ///   Microsoft.NET.Sdk
    ///   Microsoft.NET.Sdk.Web
    ///   Microsoft.NET.Sdk.BlazorWebAssembly
    ///   Microsoft.NET.Sdk.Razor
    ///   Microsoft.NET.Sdk.Worker
    ///   Microsoft.NET.Sdk.WindowsDesktop
    /// </summary>
    public string? Sdk { get; init; } = projectDocument.ChildNodes["Project"]?.Attributes[nameof(Sdk)]?.Value;

    public List<Project> ProjectReferences { get; init; } = [];

    public List<Package> PackageReferences { get; init; } = [];

    public IReadOnlyDictionary<string, string[]> SameVersionGroups { get; init; } =
        new Dictionary<string, string[]>();

    public bool IsGroupedPackage(string name) => SameVersionGroups.Values.Any(
        members => members.Contains(name, StringComparer.OrdinalIgnoreCase));

    public bool CanUpdatePackageGroup(IEnumerable<string> members) =>
        members.All(name => FindGroupVersionAttribute(projectDocument, name) != null);

    public async Task SetPackageGroupVersionAsync(IEnumerable<string> members, NugetVersion version)
    {
        var current = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        current.LoadHtml(await File.ReadAllTextAsync(PathOnDisk));
        foreach (var name in members)
        {
            var attribute = FindGroupVersionAttribute(current.DocumentNode, name)
                ?? throw new InvalidDataException($"Package '{name}' can no longer be updated as a group in '{this}'.");
            if (new NugetVersion(attribute.Value ?? string.Empty) > version)
                throw new InvalidDataException($"Group update would downgrade '{name}' in '{this}'.");
            attribute.Value = version.ToString();
        }

        await new CsprojWriter().SaveCsprojToDisk(current, PathOnDisk, atomic: true);
    }

    internal static bool IsLiteralPackageVersion(string? text) =>
        text != null && text.All(c => char.IsAsciiDigit(c) || c == '.') && System.Version.TryParse(text, out _);

    private static HtmlAttribute? FindGroupVersionAttribute(HtmlNode document, string name)
    {
        var nodes = document.Descendants("PackageReference")
            .Where(node => string.Equals(node.Attributes["Include"]?.Value, name, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (nodes.Length != 1)
            return null;

        var node = nodes[0];
        var version = node.Attributes["Version"];
        var itemGroup = node.ParentNode;
        return itemGroup?.Name == "itemgroup" && itemGroup.ParentNode?.Name == "project" &&
               node.Attributes["Condition"] == null && itemGroup.Attributes["Condition"] == null &&
               node.Attributes["VersionOverride"] == null && node.ChildNodes["Version"] == null &&
               node.ChildNodes["VersionOverride"] == null && IsLiteralPackageVersion(version?.Value)
            ? version
            : null;
    }

    public List<string> FrameworkReferences { get; init; } = [];

    public string[] GetTargetFrameworks()
    {
        if (!string.IsNullOrWhiteSpace(TargetFrameworks)) return TargetFrameworks.Split(';');

        if (!string.IsNullOrWhiteSpace(TargetFramework)) return [TargetFramework];

        return [];
    }

    public bool Executable()
    {
        return Sdk?.EndsWith("Web") ?? OutputType?.ToLowerInvariant().EndsWith("exe") ?? false;
    }

    public bool ContainsTestLibrary()
    {
        return
            PackageReferences.Any(p => p.Name.Contains("test", StringComparison.OrdinalIgnoreCase)) ||
            PackageReferences.Any(p => p.Name.Contains("xunit", StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString()
    {
        return Path.GetFileNameWithoutExtension(PathOnDisk);
    }

    public async Task SetPackageReferenceVersionAsync(string refName, NugetVersion newVersion)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);
        var node = doc.DocumentNode
            .Descendants("PackageReference")
            .FirstOrDefault(d => d.Attributes["Include"]?.Value == refName);

        if (node == null)
            throw new InvalidOperationException(
                $"Could not find PackageReference '{refName}' in project '{this}'.");

        var versionAttribute = node.Attributes["Version"]
                               ?? throw new InvalidDataException($"PackageReference '{refName}' has no Version attribute in project '{this}'.");
        if (!string.IsNullOrWhiteSpace(versionAttribute.Value))
        {
            versionAttribute.Value = newVersion.ToString();
            await new CsprojWriter().SaveCsprojToDisk(doc, PathOnDisk);
        }
    }

    public async Task ReplacePackageReferenceAsync(string refName, Package newPackage)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);
        var node = doc.DocumentNode
            .Descendants("PackageReference")
            .FirstOrDefault(d => d.Attributes["Include"]?.Value == refName);

        if (node == null)
            throw new InvalidOperationException(
                $"Could not find PackageReference '{refName}' in project '{this}'.");

        var versionAttribute = node.Attributes["Version"]
                               ?? throw new InvalidDataException($"PackageReference '{refName}' has no Version attribute in project '{this}'.");
        node.SetAttributeValue("Include", newPackage.Name);
        versionAttribute.Value = newPackage.Version.ToString();
        await new CsprojWriter().SaveCsprojToDisk(doc, PathOnDisk);
    }

    public async Task RemovePackageReferenceAsync(string refName)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);
        var node = doc.DocumentNode
            .Descendants("PackageReference")
            .FirstOrDefault(d => d.Attributes["Include"]?.Value == refName);

        if (node == null)
            throw new InvalidOperationException(
                $"Could not find PackageReference '{refName}' in project '{this}'.");

        await RemoveNodeAndSaveToDisk(node, doc);
    }

    public async Task RemoveProjectReference(string absPath)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var contextPath = Path.GetDirectoryName(PathOnDisk) ??
                          throw new IOException($"Couldn't find the project path based on: '{PathOnDisk}'.");
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);
        var node = doc.DocumentNode
            .Descendants("ProjectReference")
            .FirstOrDefault(p =>
                Equals(absPath, StringExtensions.GetAbsolutePath(contextPath, p.GetRequiredAttributeValue("Include"))));

        if (node == null)
            throw new InvalidOperationException(
                $"Could not find ProjectReference '{absPath}' in project '{this}'.");

        await RemoveNodeAndSaveToDisk(node, doc);
    }

    public async Task DeduplicateProperty(string propertyName, string correctValue)
    {
        // This method removes all but the first occurrence of a property
        // and ensures the first occurrence has the correct value.
        // It does everything in a single read-modify-save cycle.
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);

        var existingNodes = doc.DocumentNode
            .Descendants(propertyName)
            .ToArray();

        if (existingNodes.Length > 1)
        {
            // Keep the first node, update it to the correct value, remove the rest
            var firstNode = existingNodes[0];
            foreach (var node in existingNodes.Skip(1))
            {
                node.Remove();
            }

            // Update the first node's value
            if (firstNode.FirstChild is { } firstChild)
            {
                firstChild.InnerHtml = correctValue;
            }
            else
            {
                var valueNode = HtmlNode.CreateNode(correctValue);
                firstNode.AppendChild(valueNode);
            }
        }

        await new CsprojWriter().SaveCsprojToDisk(doc, PathOnDisk);
    }

    public async Task RemoveProperty(string propertyName)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);

        var existingNodes = doc.DocumentNode
            .Descendants(propertyName)
            .ToArray();

        foreach (var existingNode in existingNodes) existingNode.Remove();

        await new CsprojWriter().SaveCsprojToDisk(doc, PathOnDisk);
    }

    public async Task PackFile(string filePath)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);

        var newline = HtmlNode.CreateNode("\r\n    ");
        var itemGroup = doc.DocumentNode
            .Descendants("ItemGroup")
            .FirstOrDefault();

        if (itemGroup == null)
        {
            itemGroup = doc.CreateElement("ItemGroup");
            var projectElement = doc.GetProjectElement();
            projectElement.AppendChild(itemGroup);
            projectElement.AppendChild(newline);
        }

        var none = doc.CreateElement("None");
        none.Attributes.Add("Include", filePath);
        none.Attributes.Add("Pack", "true");
        none.Attributes.Add("PackagePath", ".");

        itemGroup.AppendChild(newline);
        itemGroup.AppendChild(none);

        await new CsprojWriter().SaveCsprojToDisk(doc, PathOnDisk);
    }

    public async Task AddOrUpdateProperty(string propertyName, string propertyValue)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);

        var existingNodes = doc.DocumentNode
            .Descendants(propertyName)
            .ToArray();
        if (existingNodes.Length != 0)
        {
            foreach (var existingNode in existingNodes)
            {
                if (existingNode.FirstChild is { } firstChild)
                {
                    firstChild.InnerHtml = propertyValue;
                }
                else
                {
                    var valueNode = HtmlNode.CreateNode(propertyValue);
                    existingNode.AppendChild(valueNode);
                }
            }
        }
        else
        {
            var newline = HtmlNode.CreateNode("\r\n    ");
            var property = doc.CreateElement(propertyName);
            property.InnerHtml = propertyValue;

            var propertyGroup = doc.DocumentNode
                .Descendants("PropertyGroup")
                .First();

            propertyGroup.AppendChild(property);
            propertyGroup.AppendChild(newline);
        }

        await new CsprojWriter().SaveCsprojToDisk(doc, PathOnDisk);
    }

    public async Task AddFrameworkReference(string frameworkReference)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);
        var newline = HtmlNode.CreateNode("\r\n    ");

        var itemGroup = doc.DocumentNode
            .Descendants("ItemGroup")
            .FirstOrDefault();

        if (itemGroup == null)
        {
            itemGroup = doc.CreateElement("ItemGroup");
            var projectElement = doc.GetProjectElement();
            projectElement.AppendChild(itemGroup);
            projectElement.AppendChild(newline);
        }

        var reference = doc.CreateElement("FrameworkReference");
        reference.Attributes.Add("Include", frameworkReference);

        itemGroup.AppendChild(newline);
        itemGroup.AppendChild(reference);
        itemGroup.AppendChild(newline);

        await new CsprojWriter().SaveCsprojToDisk(doc, PathOnDisk);
    }

    private async Task RemoveNodeAndSaveToDisk(HtmlNode node, HtmlDocument doc)
    {
        var parent = node.ParentNode
                     ?? throw new InvalidDataException($"Cannot remove detached element '{node.Name}'.");
        if (!parent.Descendants(0).Where(n => n.NodeType == HtmlNodeType.Element).Except(new[] { node }).Any())
            parent.Remove();
        else
            node.Remove();

        await new CsprojWriter().SaveCsprojToDisk(doc, PathOnDisk);
    }



    #region Build and code

    /// <summary>
    /// Can be one of the followings:
    ///
    ///   Library
    ///   Exe
    ///   WinExe
    /// </summary>
    public string? OutputType { get; init; } = projectDocument.Descendants(nameof(OutputType)).FirstOrDefault()?.FirstChild?.InnerText;

    // ReSharper disable once InconsistentNaming
    public string? UseWPF { get; init; } = projectDocument.Descendants(nameof(UseWPF)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? UseWindowsForms { get; init; } = projectDocument.Descendants(nameof(UseWindowsForms)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? Version { get; init; } = projectDocument.Descendants(nameof(Version)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? TargetFramework { get; init; } = projectDocument.Descendants(nameof(TargetFramework)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? TargetFrameworks { get; init; } = projectDocument.Descendants(nameof(TargetFrameworks)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? AssemblyName { get; init; } = projectDocument.Descendants(nameof(AssemblyName)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? RootNamespace { get; init; } = projectDocument.Descendants(nameof(RootNamespace)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? IsTestProject { get; init; } = projectDocument.Descendants(nameof(IsTestProject)).FirstOrDefault()?.FirstChild?.InnerText;

    #endregion

    #region Tool
    public string? IsPackable { get; init; } = projectDocument.Descendants(nameof(IsPackable)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? GeneratePackageOnBuild { get; init; } = projectDocument.Descendants(nameof(GeneratePackageOnBuild)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? PackAsTool { get; init; } = projectDocument.Descendants(nameof(PackAsTool)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? ToolCommandName { get; init; } = projectDocument.Descendants(nameof(ToolCommandName)).FirstOrDefault()?.FirstChild?.InnerText;

    #endregion

    #region Best practice

    public string? ImplicitUsings { get; init; } = projectDocument.Descendants(nameof(ImplicitUsings)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? Nullable { get; init; } = projectDocument.Descendants(nameof(Nullable)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? SelfContained { get; set; } = projectDocument.Descendants(nameof(SelfContained)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? PublishTrimmed { get; set; } = projectDocument.Descendants(nameof(PublishTrimmed)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? PublishReadyToRun { get; set; } = projectDocument.Descendants(nameof(PublishReadyToRun)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? PublishSingleFile { get; set; } = projectDocument.Descendants(nameof(PublishSingleFile)).FirstOrDefault()?.FirstChild?.InnerText;

    #endregion

    #region Nuget Packaging

    public string? Company { get; init; } = projectDocument.Descendants(nameof(Company)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? Product { get; init; } = projectDocument.Descendants(nameof(Product)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? Authors { get; set; } = projectDocument.Descendants(nameof(Authors)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? Description { get; init; } = projectDocument.Descendants(nameof(Description)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? PackageId { get; init; } = projectDocument.Descendants(nameof(PackageId)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? PackageTags { get; init; } = projectDocument.Descendants(nameof(PackageTags)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? PackageLicenseExpression { get; set; } = projectDocument.Descendants(nameof(PackageLicenseExpression)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? PackageProjectUrl { get; set; } = projectDocument.Descendants(nameof(PackageProjectUrl)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? RepositoryType { get; set; } = projectDocument.Descendants(nameof(RepositoryType)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? RepositoryUrl { get; set; } = projectDocument.Descendants(nameof(RepositoryUrl)).FirstOrDefault()?.FirstChild?.InnerText;
    public string? PackageReadmeFile { get; set; } = projectDocument.Descendants(nameof(PackageReadmeFile)).FirstOrDefault()?.FirstChild?.InnerText;

    #endregion
}
