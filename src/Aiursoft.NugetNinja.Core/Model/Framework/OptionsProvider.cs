using System.CommandLine;

namespace Aiursoft.NugetNinja.Core.Model.Framework;

public static class OptionsProvider
{
    public static readonly Option<string> PathOptions = new(
        name: "--path",
        aliases: ["-p"])
    {
        Description = "Path of the projects to be changed.", // 规则 2: 'Description' 是属性
        Required = true
    };

    public static readonly Option<bool> DryRunOption = new(
        name: "--dry-run",
        aliases: ["-d"])
    {
        Description = "Preview changes without actually making them" // 规则 2
    };

    public static readonly Option<bool> VerboseOption = new(
        name: "--verbose", // 规则 1
        aliases: ["-v"])
    {
        Description = "Show detailed log" // 规则 2
    };

    public static readonly Option<bool> AllowPreviewOption =
        new(
            name: "--allow-preview") // 规则 1 (这个没有别名)
        {
            Description = "Allow using preview versions of packages from Nuget." // 规则 2
        };

    public static readonly Option<string> CustomNugetServerOption =
        new(
            name: "--nuget-server") // 规则 1
        {
            Description = "If you want to use a customized nuget server instead of the official nuget.org, you can set it with a value like: https://nuget.myserver/v3/index.json" // 规则 2
        };

    public static readonly Option<string> PatTokenOption =
        new(
            name: "--token") // 规则 1
        {
            Description = "The PAT token which has privilege to access the nuget server. See: https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate" // 规则 2
        };

    public static readonly Option<bool> AllowPackageVersionCrossMicrosoftRuntime =
        new(
            name: "--allow-package-version-cross-microsoft-runtime") // 规则 1
        {
            Description = "Allow using NuGet package versions for different Microsoft runtime versions. For example, when using runtime 6.0, it will avoid upgrading packages to 7.0." // 规则 2
        };
}
