using Aiursoft.CommandFramework;
using Aiursoft.NugetNinja.AllOfficialsPlugin.App;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin.App;
using Aiursoft.NugetNinja.MissingPropertyPlugin.App;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.App;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.App;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin.App;
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
    

