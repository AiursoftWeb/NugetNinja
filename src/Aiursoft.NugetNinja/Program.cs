using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Extensions;
using Aiursoft.NugetNinja.AllOfficialsPlugin;
using Aiursoft.NugetNinja.Core;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin;
using Aiursoft.NugetNinja.MissingPropertyPlugin;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin;
using Aiursoft.NugetNinja.VisualizerPlugin;

return await new AiursoftCommand()
    .Configure(command =>
    {
        command
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
    })
    .RunAsync(args);
