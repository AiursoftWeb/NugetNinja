﻿

using System.CommandLine;

namespace Aiursoft.NugetNinja.Core;

public abstract class CommandHandler
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract void OnCommandBuilt(Command command);

    public virtual string[] Alias => Array.Empty<string>();

    public virtual CommandHandler[] GetSubCommandHandlers()
    {
        return Array.Empty<CommandHandler>();
    }

    public virtual Option[] GetOptions()
    {
        return Array.Empty<Option>();
    }

    public virtual Command BuildAsCommand()
    {
        var command = new Command(Name, Description);
        foreach (var alias in Alias)
        {
            command.AddAlias(alias);
        }

        foreach (var option in GetOptions())
        {
            command.AddOption(option);
        }

        OnCommandBuilt(command);

        return command;
    }
}
