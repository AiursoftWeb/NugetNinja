using System.CommandLine;

namespace Aiursoft.NugetNinja.Core;

public static class OptionsProvider
{
    public static readonly Option<string> PathOptions = new(
        new[] { "--path", "-p" },
        "Path of the projects to be changed.")
    {
        IsRequired = true
    };

    public static readonly Option<bool> DryRunOption = new(
        new[] { "--dry-run", "-d" },
        "Preview changes without actually making them");

    public static readonly Option<bool> VerboseOption = new(
        new[] { "--verbose", "-v" },
        "Show detailed log");

    public static readonly Option<bool> AllowPreviewOption =
        new(
            new[] { "--allow-preview" },
            "Allow using preview versions of packages from Nuget.");

    public static readonly Option<string> CustomNugetServer =
        new(
            new[] { "--nuget-server" },
            "If you want to use a customized nuget server instead of the official nuget.org, you can set it with a value like: https://nuget.myserver/v3/index.json");

    public static readonly Option<string> PatToken =
        new(
            new[] { "--token" },
            "The PAT token which has privilege to access the nuget server. See: https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate");

    private static Option[] GetGlobalOptions()
    {
        return new Option[]
        {
            PathOptions,
            DryRunOption,
            VerboseOption,
            AllowPreviewOption,
            CustomNugetServer,
            PatToken
        };
    }

    public static RootCommand AddGlobalOptions(this RootCommand command)
    {
        var options = GetGlobalOptions();
        foreach (var option in options) command.AddGlobalOption(option);
        return command;
    }

    public static RootCommand AddPlugins(this RootCommand command, params INinjaPlugin[] pluginInstallers)
    {
        foreach (var plugin in pluginInstallers)
        foreach (var pluginFeature in plugin.Install())
            command.Add(pluginFeature.BuildAsCommand());
        return command;
    }
}