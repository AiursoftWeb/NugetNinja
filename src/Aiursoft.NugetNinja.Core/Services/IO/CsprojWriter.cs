using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace Aiursoft.NugetNinja.Core.Services.IO;

public class CsprojWriter
{
    private static readonly string[] PropertyOrder =
    [
        "OutputType",
        "Version",
        "TargetFramework",
        "AssemblyName",
        "RootNamespace",
        "UseWPF",
        "UseWindowsForms",
        "IsTestProject",
        "GeneratePackageOnBuild",
        "IsPackable",
        "PackAsTool",
        "ToolCommandName",
        "ImplicitUsings",
        "Nullable",
        "SelfContained",
        "PublishTrimmed",
        "PublishReadyToRun",
        "PublishSingleFile",
        "Company",
        "Product",
        "Authors",
        "Description",
        "PackageId",
        "PackageTags",
        "PackageLicenseExpression",
        "PackageProjectUrl",
        "RepositoryType",
        "RepositoryUrl",
        "PackageReadmeFile"
    ];
    
    public async Task SaveCsprojToDisk(HtmlDocument doc, string path)
    {
        Sort(doc);
        var memoryStream = new MemoryStream();
        doc.Save(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);
        var csprojText = await new StreamReader(memoryStream).ReadToEndAsync();
        csprojText = csprojText
            .Replace(@"></PackageReference>", " />")
            .Replace(@"></ProjectReference>", " />")
            .Replace(@"></FrameworkReference>", " />")
            .Replace(@"></Compile>", " />")
            .Replace(@"></Content>", " />")
            .Replace(@"></None>", " />")
            .Replace(@"></Exec>", " />")
            .Replace(@"></Output>", " />")
            .Replace(@"></Message>", " />")
            .Replace(@"></Watch>", " />")
            .Replace(@"></Resource>", " />")
            .Replace(@"></Folder>", " />")
            .Replace(@"></AdditionalFiles>", " />")
            .Replace(@"></UserProperties>", " />")
            .Replace(@"></EmbeddedResource>", " />")
            .Replace(@"></ServiceWorker>", " />")
            .Replace(@"></Using>", " />");

        var indentedText = FormatXml(csprojText);
        await File.WriteAllTextAsync(path, indentedText);
    }

    private string FormatXml(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                OmitXmlDeclaration = true
            };
            var sw = new StringWriter();
            using (var writer = XmlWriter.Create(sw, settings))
            {
                doc.Save(writer);
            }

            return sw.ToString();
        }
        catch (Exception)
        {
            // Handle and throw if fatal exception here; don't just ignore them
            return xml;
        }
    }

    private void Sort(HtmlDocument doc)
    {
        var propertyGroup = doc.DocumentNode.Descendants("PropertyGroup").FirstOrDefault();
        if (propertyGroup == null)
        {
            throw new InvalidOperationException("Can not find PropertyGroup node in the .csproj file!");
        }

        var properties = propertyGroup.Descendants().Where(n => n.NodeType == HtmlNodeType.Element).ToList();
        var sortedProperties = SortProperties(properties);

        propertyGroup.RemoveAllChildren();
        foreach (var property in sortedProperties)
        {
            propertyGroup.AppendChild(property);
        }
    }

    private List<HtmlNode> SortProperties(List<HtmlNode> properties)
    {
        var sortedProperties = new List<HtmlNode>();
        var remainingProperties = new List<HtmlNode>(properties);

        foreach (var propertyName in PropertyOrder)
        {
            var property =
                remainingProperties.FirstOrDefault(p =>
                    p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                sortedProperties.Add(property);
                remainingProperties.Remove(property);
            }
        }

        sortedProperties.AddRange(remainingProperties);

        return sortedProperties;
    }
}