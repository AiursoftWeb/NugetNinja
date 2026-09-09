using HtmlAgilityPack;

namespace Aiursoft.NugetNinja.Core.Services.Utils;

public static class HtmlNodeExtensions
{
    public static string GetRequiredAttributeValue(this HtmlNode node, string name)
    {
        return node.Attributes[name]?.Value
               ?? throw new InvalidDataException($"Element '{node.Name}' is missing required attribute '{name}'.");
    }

    public static HtmlNode GetProjectElement(this HtmlDocument document)
    {
        return document.DocumentNode.ChildNodes["Project"]
               ?? throw new InvalidDataException("The project document is missing its Project element.");
    }
}
