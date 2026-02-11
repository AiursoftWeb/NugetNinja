using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.DuplicatePropertyPlugin.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.DuplicatePropertyPlugin.Services;

public class DuplicatePropertyDetector(ILogger<DuplicatePropertyDetector> logger) : IActionDetector
{
    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        foreach (var project in context.AllProjects)
        {
            var csprojContent = await File.ReadAllTextAsync(project.PathOnDisk);
            var doc = new HtmlDocument();
            doc.LoadHtml(csprojContent);
            
            var propertyGroups = doc.DocumentNode.Descendants("PropertyGroup").ToList();
            foreach (var propertyGroup in propertyGroups)
            {
                var properties = propertyGroup.ChildNodes
                    .Where(n => n.NodeType == HtmlNodeType.Element)
                    .GroupBy(n => n.Name)
                    .Where(g => g.Count() > 1)
                    .ToList();

                foreach (var group in properties)
                {
                    var propertyName = group.Key;
                    var firstValue = group.First().InnerText;
                    
                    logger.LogTrace("Project {Project} has duplicate property {PropertyName}", project, propertyName);
                    yield return new DuplicateProperty(project, propertyName, firstValue);
                }
            }
        }
    }
}