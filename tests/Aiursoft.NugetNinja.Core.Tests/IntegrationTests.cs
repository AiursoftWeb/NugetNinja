using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class IntegrationTests
{
    private readonly AiursoftCommand _command;

    public IntegrationTests()
    {
        _command = new AiursoftCommand().Configure(command => command.AddGlobalOptions().AddPlugins(                
            new AllOfficialsPlugin.AllOfficialsPlugin(),
            new MissingPropertyPlugin.MissingPropertyPlugin(),
            new DeprecatedPackagePlugin.DeprecatedPackagePlugin(),
            new PossiblePackageUpgradePlugin.PossiblePackageUpgradePlugin(),
            new UselessPackageReferencePlugin.UselessPackageReferencePlugin(),
            new UselessProjectReferencePlugin.UselessProjectReferencePlugin(),
            new VisualizerPlugin.VisualizerPlugin()));
    }

    [TestMethod]
    public async Task InvokeHelp()
    {
        var result = await _command.TestRunAsync(new[] { "--help" });

        Assert.AreEqual(0, result.ProgramReturn);
        Assert.IsTrue(result.Output.Contains("Options:"));
        Assert.IsTrue(string.IsNullOrWhiteSpace(result.Error));
    }

    [TestMethod]
    public async Task InvokeVersion()
    {
        var result = await _command.TestRunAsync(new[] { "--version" });
        Assert.AreEqual(0, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeUnknown()
    {
        var result = await _command.TestRunAsync(new[] { "--wtf" });
        Assert.AreEqual(1, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeWithoutArg()
    {
        var result = await _command.TestRunAsync(Array.Empty<string>());
        Assert.AreEqual(1, result.ProgramReturn);
    }
}
