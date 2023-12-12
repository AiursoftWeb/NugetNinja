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

return await new NestedCommandApp()
    .WithFeature(new AllOfficialsHandler())
    .WithFeature(new MissingPropertyHandler())
    .WithFeature(new DeprecatedPackageHandler())
    .WithFeature(new PackageUpgradeHandler())
    .WithFeature(new PackageReferenceHandler())
    .WithFeature(new ProjectReferenceHandler())
    .WithFeature(new VisualizerHandler())
    .WithGlobalOptions(OptionsProvider.PathOptions)
    .WithGlobalOptions(OptionsProvider.DryRunOption)
    .WithGlobalOptions(OptionsProvider.VerboseOption)
    .WithGlobalOptions(OptionsProvider.AllowPreviewOption)
    .WithGlobalOptions(OptionsProvider.CustomNugetServerOption)
    .WithGlobalOptions(OptionsProvider.PatTokenOption)
    .WithGlobalOptions(OptionsProvider.AllowPackageVersionCrossMicrosoftRuntime)
    .RunAsync(args);
    

