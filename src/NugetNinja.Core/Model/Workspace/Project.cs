using Aiursoft.NugetNinja.Core.Services;
using HtmlAgilityPack;

namespace Aiursoft.NugetNinja.Core;

public class Project
{
    public Project(string pathOnDisk, HtmlNode doc)
    {
        // Global
        PathOnDisk = pathOnDisk;
        Sdk = doc.ChildNodes["Project"].Attributes[nameof(Sdk)]?.Value;
        
        // Build and code
        OutputType = doc.Descendants(nameof(OutputType)).SingleOrDefault()?.FirstChild?.InnerText;
        Version = doc.Descendants(nameof(Version)).SingleOrDefault()?.FirstChild?.InnerText;
        TargetFramework = doc.Descendants(nameof(TargetFramework)).SingleOrDefault()?.FirstChild?.InnerText;
        TargetFrameworks = doc.Descendants(nameof(TargetFrameworks)).SingleOrDefault()?.FirstChild?.InnerText;
        AssemblyName = doc.Descendants(nameof(AssemblyName)).SingleOrDefault()?.FirstChild?.InnerText;
        RootNamespace = doc.Descendants(nameof(RootNamespace)).SingleOrDefault()?.FirstChild?.InnerText;
        IsTestProject = doc.Descendants(nameof(IsTestProject)).SingleOrDefault()?.FirstChild?.InnerText;

        // Tool
        IsPackable = doc.Descendants(nameof(IsPackable)).SingleOrDefault()?.FirstChild?.InnerText;
        PackAsTool = doc.Descendants(nameof(PackAsTool)).SingleOrDefault()?.FirstChild?.InnerText;
        ToolCommandName = doc.Descendants(nameof(ToolCommandName)).SingleOrDefault()?.FirstChild?.InnerText;

        // Best practice
        ImplicitUsings = doc.Descendants(nameof(ImplicitUsings)).SingleOrDefault()?.FirstChild?.InnerText;
        Nullable = doc.Descendants(nameof(Nullable)).SingleOrDefault()?.FirstChild?.InnerText;
        SelfContained = doc.Descendants(nameof(SelfContained)).SingleOrDefault()?.FirstChild?.InnerText;
        PublishTrimmed = doc.Descendants(nameof(PublishTrimmed)).SingleOrDefault()?.FirstChild?.InnerText;
        PublishReadyToRun = doc.Descendants(nameof(PublishReadyToRun)).SingleOrDefault()?.FirstChild?.InnerText;
        PublishSingleFile = doc.Descendants(nameof(PublishSingleFile)).SingleOrDefault()?.FirstChild?.InnerText;
        
        // Nuget
        Company = doc.Descendants(nameof(Company)).SingleOrDefault()?.FirstChild?.InnerText;
        Product = doc.Descendants(nameof(Product)).SingleOrDefault()?.FirstChild?.InnerText;
        Authors = doc.Descendants(nameof(Authors)).SingleOrDefault()?.FirstChild?.InnerText;
        Description = doc.Descendants(nameof(Description)).SingleOrDefault()?.FirstChild?.InnerText;
        PackageId = doc.Descendants(nameof(PackageId)).SingleOrDefault()?.FirstChild?.InnerText;
        PackageTags = doc.Descendants(nameof(PackageTags)).SingleOrDefault()?.FirstChild?.InnerText;
        PackageLicenseExpression = doc.Descendants(nameof(PackageLicenseExpression)).SingleOrDefault()?.FirstChild?.InnerText;
        PackageProjectUrl = doc.Descendants(nameof(PackageProjectUrl)).SingleOrDefault()?.FirstChild?.InnerText;
        RepositoryType = doc.Descendants(nameof(RepositoryType)).SingleOrDefault()?.FirstChild?.InnerText;
        RepositoryUrl = doc.Descendants(nameof(RepositoryUrl)).SingleOrDefault()?.FirstChild?.InnerText;
    }

    public string PathOnDisk { get; set; }
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
    public string? Sdk { get; init; }

    public List<Project> ProjectReferences { get; init; } = new();

    public List<Package> PackageReferences { get; init; } = new();

    public List<string> FrameworkReferences { get; init; } = new();

    public string[] GetTargetFrameworks()
    {
        if (!string.IsNullOrWhiteSpace(TargetFrameworks)) return TargetFrameworks.Split(';');

        if (!string.IsNullOrWhiteSpace(TargetFramework)) return new[] { TargetFramework };

        return Array.Empty<string>();
    }

    public bool Executable()
    {
        return Sdk?.EndsWith("Web") ?? OutputType?.ToLower().EndsWith("exe") ?? false;
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
            .FirstOrDefault(d => d.Attributes["Include"].Value == refName);

        if (node == null)
            throw new InvalidOperationException(
                $"Could remove PackageReference {refName} in project {this} because it was not found!");

        if (node.Attributes["Version"] != null)
        {
            node.Attributes["Version"].Value = newVersion.ToString();
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
            .FirstOrDefault(d => d.Attributes["Include"].Value == refName);

        if (node == null)
            throw new InvalidOperationException(
                $"Could remove PackageReference {refName} in project {this} because it was not found!");

        node.Attributes["Include"].Value = newPackage.Name;
        node.Attributes["Version"].Value = newPackage.Version.ToString();
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
            .FirstOrDefault(d => d.Attributes["Include"].Value == refName);

        if (node == null)
            throw new InvalidOperationException(
                $"Could remove PackageReference {refName} in project {this} because it was not found!");

        await RemoveNodeAndSaveToDisk(node, doc);
    }

    public async Task RemoveProjectReference(string absPath)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var contextPath = Path.GetDirectoryName(PathOnDisk) ??
                          throw new IOException($"Couldn't find the project path based on: '{PathOnDisk}'.");
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true
        };
        doc.LoadHtml(csprojContent);
        var node = doc.DocumentNode
            .Descendants("ProjectReference")
            .FirstOrDefault(p =>
                Equals(absPath, StringExtensions.GetAbsolutePath(contextPath, p.Attributes["Include"].Value)));

        if (node == null)
            throw new InvalidOperationException(
                $"Could remove PackageReference {absPath} in project {this} because it was not found!");

        await RemoveNodeAndSaveToDisk(node, doc);
    }

    public async Task RemoveProperty(string propertyName)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true
        };
        doc.LoadHtml(csprojContent);

        var existingNodes = doc.DocumentNode
            .Descendants(propertyName)
            .ToArray();

        foreach (var existingNode in existingNodes) existingNode.Remove();

        await new CsprojWriter().SaveCsprojToDisk(doc, PathOnDisk);
    }

    public async Task AddOrUpdateProperty(string propertyName, string propertyValue)
    {
        var csprojContent = await File.ReadAllTextAsync(PathOnDisk);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true
        };
        doc.LoadHtml(csprojContent);

        var existingNodes = doc.DocumentNode
            .Descendants(propertyName)
            .ToArray();
        if (existingNodes.Any())
        {
            foreach (var existingNode in existingNodes)
                if (existingNode.FirstChild != null)
                {
                    existingNode.FirstChild.InnerHtml = propertyValue;
                }
                else
                {
                    var valueNode = HtmlNode.CreateNode(propertyValue);
                    existingNode.AppendChild(valueNode);
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
            OptionOutputOriginalCase = true
        };
        doc.LoadHtml(csprojContent);
        var newline = HtmlNode.CreateNode("\r\n    ");

        var itemGroup = doc.DocumentNode
            .Descendants("ItemGroup")
            .FirstOrDefault();

        if (itemGroup == null)
        {
            itemGroup = doc.CreateElement("ItemGroup");
            doc.DocumentNode.FirstChild?.AppendChild(itemGroup);
            doc.DocumentNode.FirstChild?.AppendChild(newline);
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
        var parent = node.ParentNode;
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
    public string? OutputType { get; init; }
    public string? Version { get; init; }
    public string? TargetFramework { get; init; }
    public string? TargetFrameworks { get; init; }
    public string? AssemblyName { get; init; }
    public string? RootNamespace { get; init; }
    public string? IsTestProject { get; init; }
    #endregion
    
    #region Tool
    public string? IsPackable { get; init; }
    public string? PackAsTool { get; init; }
    public string? ToolCommandName { get; init; }

    #endregion

    #region Best practice

    public string? ImplicitUsings { get; init; }
    public string? Nullable { get; init; }
    public string? SelfContained { get; set; }
    public string? PublishTrimmed { get; set; }
    public string? PublishReadyToRun { get; set; }
    public string? PublishSingleFile { get; set; }

    #endregion

    #region Nuget Packaging

    public string? Company { get; init; }
    public string? Product { get; init; }
    public string? Authors { get; set; }
    public string? Description { get; init; }
    public string? PackageId { get; init; }
    public string? PackageTags { get; init; }
    public string? PackageLicenseExpression { get; set; }
    public string? PackageProjectUrl { get; set; }
    public string? RepositoryType { get; set; }
    public string? RepositoryUrl { get; set; }

    #endregion
}