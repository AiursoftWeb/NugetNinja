using System.CommandLine;
using Aiursoft.CommandFramework.Extensions;
using Aiursoft.NugetNinja.AllOfficialsPlugin;
using Aiursoft.NugetNinja.Core;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin;
using Aiursoft.NugetNinja.MissingPropertyPlugin;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin;
using Aiursoft.NugetNinja.VisualizerPlugin;

var description = "Nuget Ninja, a tool for detecting dependencies of .NET projects.";

var program = new RootCommand(description)
    .AddGlobalOptions()
    .AddPlugins(
        new AllOfficialsPlugin(),
        new MissingPropertyPlugin(),
        new DeprecatedPackagePlugin(),
        new PossiblePackageUpgradePlugin(),
        new UselessPackageReferencePlugin(),
        new UselessProjectReferencePlugin(),
        new VisualizerPlugin()
    );

return await program.InvokeAsync(args);