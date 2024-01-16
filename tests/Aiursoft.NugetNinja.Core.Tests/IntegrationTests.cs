using Aiursoft.CommandFramework;
using Aiursoft.NugetNinja.AllOfficialsPlugin;
using Aiursoft.NugetNinja.AllOfficialsPlugin.App;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin.App;
using Aiursoft.NugetNinja.MissingPropertyPlugin;
using Aiursoft.NugetNinja.MissingPropertyPlugin.App;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.App;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.App;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin.App;
using Aiursoft.NugetNinja.VisualizerPlugin;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class IntegrationTests
{
    private readonly NestedCommandApp _program = new NestedCommandApp()
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
        .WithGlobalOptions(OptionsProvider.AllowPackageVersionCrossMicrosoftRuntime);

    [TestMethod]
    public async Task InvokeHelp()
    {
        var result = await _program.TestRunAsync(new[] { "--help" });

        Assert.AreEqual(0, result.ProgramReturn);
        Assert.IsTrue(result.Output.Contains("Options:"));
        Assert.IsTrue(string.IsNullOrWhiteSpace(result.Error));
    }

    [TestMethod]
    public async Task InvokeVersion()
    {
        var result = await _program.TestRunAsync(new[] { "--version" });
        Assert.AreEqual(0, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeUnknown()
    {
        var result = await _program.TestRunAsync(new[] { "--wtf" });
        Assert.AreEqual(1, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeWithoutArg()
    {
        var result = await _program.TestRunAsync(Array.Empty<string>());
        Assert.AreEqual(1, result.ProgramReturn);
    }
}
