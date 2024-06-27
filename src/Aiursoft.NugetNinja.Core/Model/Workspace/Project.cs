using Aiursoft.NugetNinja.Core.Services.IO;
using Aiursoft.NugetNinja.Core.Services.Utils;
using HtmlAgilityPack;

namespace Aiursoft.NugetNinja.Core.Model.Workspace;

public class Project(string pathOnDisk, HtmlNode doc)
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
    public string? Sdk { get; init; } = doc.ChildNodes["Project"].Attributes[nameof(Sdk)]?.Value;

    public List<Project> ProjectReferences { get; init; } = [];

    public List<Package> PackageReferences { get; init; } = [];

    public List<string> FrameworkReferences { get; init; } = [];

    public string[] GetTargetFrameworks()
    {
        if (!string.IsNullOrWhiteSpace(TargetFrameworks)) return TargetFrameworks.Split(';');

        if (!string.IsNullOrWhiteSpace(TargetFramework)) return [TargetFramework];

        return [];
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

    public async Task PackFile(string filePath)
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
    public string? OutputType { get; init; } = doc.Descendants(nameof(OutputType)).SingleOrDefault()?.FirstChild?.InnerText;

    // ReSharper disable once InconsistentNaming
    public string? UseWPF { get; init; } = doc.Descendants(nameof(UseWPF)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? UseWindowsForms { get; init; } = doc.Descendants(nameof(UseWindowsForms)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? Version { get; init; } = doc.Descendants(nameof(Version)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? TargetFramework { get; init; } = doc.Descendants(nameof(TargetFramework)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? TargetFrameworks { get; init; } = doc.Descendants(nameof(TargetFrameworks)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? AssemblyName { get; init; } = doc.Descendants(nameof(AssemblyName)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? RootNamespace { get; init; } = doc.Descendants(nameof(RootNamespace)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? IsTestProject { get; init; } = doc.Descendants(nameof(IsTestProject)).SingleOrDefault()?.FirstChild?.InnerText;

    #endregion
    
    #region Tool
    public string? IsPackable { get; init; } = doc.Descendants(nameof(IsPackable)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? GeneratePackageOnBuild { get; init; } = doc.Descendants(nameof(GeneratePackageOnBuild)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? PackAsTool { get; init; } = doc.Descendants(nameof(PackAsTool)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? ToolCommandName { get; init; } = doc.Descendants(nameof(ToolCommandName)).SingleOrDefault()?.FirstChild?.InnerText;

    #endregion

    #region Best practice

    public string? ImplicitUsings { get; init; } = doc.Descendants(nameof(ImplicitUsings)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? Nullable { get; init; } = doc.Descendants(nameof(Nullable)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? SelfContained { get; set; } = doc.Descendants(nameof(SelfContained)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? PublishTrimmed { get; set; } = doc.Descendants(nameof(PublishTrimmed)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? PublishReadyToRun { get; set; } = doc.Descendants(nameof(PublishReadyToRun)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? PublishSingleFile { get; set; } = doc.Descendants(nameof(PublishSingleFile)).SingleOrDefault()?.FirstChild?.InnerText;

    #endregion

    #region Nuget Packaging

    public string? Company { get; init; } = doc.Descendants(nameof(Company)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? Product { get; init; } = doc.Descendants(nameof(Product)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? Authors { get; set; } = doc.Descendants(nameof(Authors)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? Description { get; init; } = doc.Descendants(nameof(Description)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? PackageId { get; init; } = doc.Descendants(nameof(PackageId)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? PackageTags { get; init; } = doc.Descendants(nameof(PackageTags)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? PackageLicenseExpression { get; set; } = doc.Descendants(nameof(PackageLicenseExpression)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? PackageProjectUrl { get; set; } = doc.Descendants(nameof(PackageProjectUrl)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? RepositoryType { get; set; } = doc.Descendants(nameof(RepositoryType)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? RepositoryUrl { get; set; } = doc.Descendants(nameof(RepositoryUrl)).SingleOrDefault()?.FirstChild?.InnerText;
    public string? PackageReadmeFile { get; set; } = doc.Descendants(nameof(PackageReadmeFile)).SingleOrDefault()?.FirstChild?.InnerText;

    #endregion
}