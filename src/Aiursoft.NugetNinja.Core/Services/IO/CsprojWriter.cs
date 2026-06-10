using System.Text.RegularExpressions;
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

    private static readonly string[] SelfClosingElements =
    [
        "PackageReference",
        "ProjectReference",
        "FrameworkReference",
        "Compile",
        "Content",
        "None",
        "Exec",
        "Output",
        "Message",
        "Watch",
        "Resource",
        "Folder",
        "AdditionalFiles",
        "UserProperties",
        "EmbeddedResource",
        "ServiceWorker",
        "Using"
    ];

    public async Task SaveCsprojToDisk(HtmlDocument doc, string path)
    {
        SortAllPropertyGroups(doc);
        var memoryStream = new MemoryStream();
        doc.Save(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);
        var csprojText = await new StreamReader(memoryStream).ReadToEndAsync();

        // Apply self-closing tag replacements for known empty elements
        foreach (var element in SelfClosingElements)
        {
            csprojText = csprojText.Replace($"></{element}>", " />");
        }

        // Fix HtmlAgilityPack bug: underscore-prefixed XML elements (like _Parameter1)
        // lose their closing tags during Save. HtmlAgilityPack doesn't recognize
        // underscore-prefixed names as valid HTML/XML tags.
        csprojText = Regex.Replace(
            csprojText,
            @"<(_\w+)>([^<]+)(?!\s*</\1>)",
            @"<$1>$2</$1>");

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
                OmitXmlDeclaration = true,
                NewLineChars = "\n"
            };
            var sw = new StringWriter();
            using (var writer = XmlWriter.Create(sw, settings))
            {
                doc.Save(writer);
            }

            var result = sw.ToString();
            // XmlWriter adds a trailing newline; ensure we have exactly one
            return result.TrimEnd('\n') + "\n";
        }
        catch (XmlException ex)
        {
            // If XDocument can't parse the HTML output, it means the document is malformed.
            // This should never happen in normal operation, but HtmlAgilityPack may produce
            // invalid XML for edge-case constructs. Try to salvage by re-parsing with
            // HtmlAgilityPack and re-saving with proper options.
            try
            {
                var recoveryDoc = new HtmlDocument
                {
                    OptionOutputOriginalCase = true,
                    OptionAutoCloseOnEnd = true,
                    OptionWriteEmptyNodes = true
                };
                recoveryDoc.LoadHtml(xml);
                var recoveryStream = new MemoryStream();
                recoveryDoc.Save(recoveryStream);
                recoveryStream.Seek(0, SeekOrigin.Begin);
                var recovered = new StreamReader(recoveryStream).ReadToEnd();

                // Try to parse recovered output
                var xdoc = XDocument.Parse(recovered);
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    OmitXmlDeclaration = true,
                    NewLineChars = "\n"
                };
                var sw = new StringWriter();
                using (var writer = XmlWriter.Create(sw, settings))
                {
                    xdoc.Save(writer);
                }
                var result = sw.ToString();
                return result.TrimEnd('\n') + "\n";
            }
            catch
            {
                // Last resort: return the original XML with a warning.
                // We must not silently corrupt the file.
                Console.Error.WriteLine(
                    $"[NugetNinja Warning] CsprojWriter failed to format XML. " +
                    $"Original parse error: {ex.Message}. The output may not be valid XML.");
                return xml;
            }
        }
    }

    private void SortAllPropertyGroups(HtmlDocument doc)
    {
        var propertyGroups = doc.DocumentNode.Descendants("PropertyGroup").ToList();
        if (propertyGroups.Count == 0)
        {
            throw new InvalidOperationException("Can not find PropertyGroup node in the .csproj file!");
        }

        foreach (var propertyGroup in propertyGroups)
        {
            SortPropertyGroupChildren(propertyGroup);
        }
    }

    private void SortPropertyGroupChildren(HtmlNode propertyGroup)
    {
        // Only sort direct child elements, not nested descendants.
        // Using Descendants() would pull up nested elements from deeper levels,
        // which can corrupt elements like AssemblyAttribute > _Parameter1
        // if the HTML parser incorrectly nests them under a PropertyGroup.
        var properties = propertyGroup.ChildNodes
            .Where(n => n.NodeType == HtmlNodeType.Element)
            .ToList();

        // Preserve text nodes (whitespace) between elements
        var allNodes = propertyGroup.ChildNodes.ToList();
        var textNodes = allNodes.Where(n => n.NodeType == HtmlNodeType.Text).ToList();

        var sortedProperties = SortProperties(properties);

        propertyGroup.RemoveAllChildren();

        // Re-add elements in sorted order, interspersing with preserved text nodes.
        // The first text node (newline+indent after <PropertyGroup>) is used for
        // the leading gap and between elements. The last text node (newline+indent
        // before </PropertyGroup>, may have different indentation) trails the elements.
        var indentTextNode = textNodes.FirstOrDefault();
        var trailingTextNode = textNodes.LastOrDefault();

        // Leading newline after <PropertyGroup>
        if (indentTextNode != null)
            propertyGroup.AppendChild(indentTextNode.Clone());

        for (var i = 0; i < sortedProperties.Count; i++)
        {
            propertyGroup.AppendChild(sortedProperties[i]);
            if (i < sortedProperties.Count - 1 && indentTextNode != null)
                propertyGroup.AppendChild(indentTextNode.Clone());
        }

        // Trailing newline before </PropertyGroup>
        if (trailingTextNode != null)
            propertyGroup.AppendChild(trailingTextNode.Clone());
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
