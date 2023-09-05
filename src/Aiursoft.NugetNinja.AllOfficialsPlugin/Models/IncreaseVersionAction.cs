﻿using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin.Models;

public class IncreaseVersionAction : IAction
{
    public IncreaseVersionAction(Project source, NugetVersion newVersion)
    {
        SourceProject = source;
        NewVersion = newVersion;
    }

    public Project SourceProject { get; }
    public NugetVersion NewVersion { get; }

    public string BuildMessage()
    {
        return
            $"The project: '{SourceProject}' should release a new version because it is updated!";
    }

    public Task TakeActionAsync()
    {
        return SourceProject.AddOrUpdateProperty("Version", NewVersion.ToString());
    }
}