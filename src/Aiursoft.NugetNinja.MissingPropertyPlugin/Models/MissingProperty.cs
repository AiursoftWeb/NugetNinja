using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin.Models;

public class PackFile(Project csproj, string filePath) : IAction
{
    public Project SourceProject { get; } = csproj;

    public string BuildMessage()
    {
        return $"The project: '{SourceProject}' should also pack with file: {filePath}.";
    }

    public Task TakeActionAsync()
    {
        return SourceProject.PackFile(filePath);
    }

}

public class MissingProperty(Project csproj, string propertyName, string suggestedValue, string? currentValue = null)
    : IAction
{
    public Project SourceProject { get; } = csproj;

    public string BuildMessage()
    {
        if (currentValue != suggestedValue)
        {
            if (string.IsNullOrWhiteSpace(currentValue))
            {
                return
                    $"The project: '{SourceProject}' lacks of property '{propertyName}'. You can possibly set that to: '{suggestedValue}'.";
            }
            else
            {
                return $"The project: '{SourceProject}' property '{propertyName}' with value '{currentValue}' was not suggested. You can possibly set that to: '{suggestedValue}'.";
            }
        }

        return string.Empty;
    }

    public Task TakeActionAsync()
    {
        return SourceProject.AddOrUpdateProperty(propertyName, suggestedValue);
    }
}