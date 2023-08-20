﻿using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class InsertFrameworkReference : IAction
{
    public InsertFrameworkReference(Project source, string frameworkReference)
    {
        SourceProject = source;
        FrameworkReference = frameworkReference;
    }

    public Project SourceProject { get; set; }
    public string FrameworkReference { get; set; }

    public string BuildMessage()
    {
        return $"The project: '{SourceProject}' may need to reference framework: {FrameworkReference}.";
    }

    public Task TakeActionAsync()
    {
        return SourceProject.AddFrameworkReference(FrameworkReference);
    }
}