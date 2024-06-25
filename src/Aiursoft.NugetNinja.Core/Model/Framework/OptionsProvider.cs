using System.CommandLine;

namespace Aiursoft.NugetNinja.Core.Model.Framework;

public static class OptionsProvider
{
    public static readonly Option<string> PathOptions = new(
        ["--path", "-p"],
        "Path of the projects to be changed.")
    {
        IsRequired = true
    };

    public static readonly Option<bool> DryRunOption = new(
        ["--dry-run", "-d"],
        "Preview changes without actually making them");

    public static readonly Option<bool> VerboseOption = new(
        ["--verbose", "-v"],
        "Show detailed log");

    public static readonly Option<bool> AllowPreviewOption =
        new(
            ["--allow-preview"],
            "Allow using preview versions of packages from Nuget.");

    public static readonly Option<string> CustomNugetServerOption =
        new(
            ["--nuget-server"],
            "If you want to use a customized nuget server instead of the official nuget.org, you can set it with a value like: https://nuget.myserver/v3/index.json");

    public static readonly Option<string> PatTokenOption =
        new(
            ["--token"],
            "The PAT token which has privilege to access the nuget server. See: https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate");

    public static readonly Option<bool> AllowPackageVersionCrossMicrosoftRuntime =
        new(
            ["--allow-package-version-cross-microsoft-runtime"],
            "Allow using NuGet package versions for different Microsoft runtime versions. For example, when using runtime 6.0, it will avoid upgrading packages to 7.0.");
}