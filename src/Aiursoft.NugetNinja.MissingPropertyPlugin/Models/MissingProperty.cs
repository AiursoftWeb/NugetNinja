﻿using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class MissingProperty : IAction
{
    private readonly string? _currentValue;
    private readonly string _propertyName;
    private readonly string _suggestedValue;

    public Project SourceProject { get; }

    public MissingProperty(Project csproj, string propertyName, string suggestedValue, string? currentValue = null)
    {
        SourceProject = csproj;
        _propertyName = propertyName;
        _suggestedValue = suggestedValue;
        _currentValue = currentValue;
    }

    public string BuildMessage()
    {
        if (_currentValue != _suggestedValue)
        {
            if (string.IsNullOrWhiteSpace(_currentValue))
            {
                return
                    $"The project: '{SourceProject}' lacks of property '{_propertyName}'. You can possibly set that to: '{_suggestedValue}'.";
            }
            else
            {
                return $"The project: '{SourceProject}' property '{_propertyName}' with value '{_currentValue}' was not suggested. You can possibly set that to: '{_suggestedValue}'.";
            }
        }

        return string.Empty;
    }

    public Task TakeActionAsync()
    {
        return SourceProject.AddOrUpdateProperty(_propertyName, _suggestedValue);
    }
}