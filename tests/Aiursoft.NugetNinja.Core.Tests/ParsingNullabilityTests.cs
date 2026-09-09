using System.Xml.Linq;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Microsoft.Extensions.Logging.Abstractions;
using WorkspaceModel = Aiursoft.NugetNinja.Core.Model.Workspace.Model;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class ParsingNullabilityTests
{
    private string _directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Directory.CreateTempSubdirectory("ninja-nullability-").FullName;
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_directory, true);
    }

    private async Task<Project> ParseProject(string content)
    {
        var path = Path.Combine(_directory, "Sample.csproj");
        await File.WriteAllTextAsync(path, content);
        return await new WorkspaceModel().IncludeProject(path, NullLogger.Instance);
    }

    [TestMethod]
    public async Task OptionalSdkAndEmptyPropertiesCanBeRead()
    {
        var project = await ParseProject("""
            <Project>
              <PropertyGroup>
                <Version></Version>
                <TargetFramework></TargetFramework>
                <Description></Description>
              </PropertyGroup>
            </Project>
            """);

        Assert.IsNull(project.Sdk);
        Assert.IsNull(project.Version);
        Assert.IsNull(project.Description);
        Assert.IsEmpty(project.GetTargetFrameworks());
        await project.AddOrUpdateProperty("Version", "1.2.3");
        var document = XDocument.Load(project.PathOnDisk);
        Assert.AreEqual("1.2.3", document.Descendants("Version").Single().Value);
    }

    [TestMethod]
    [DataRow("<PackageReference Version=\"1.0.0\" />", "Include")]
    [DataRow("<PackageReference Include=\"Example\" />", "Version")]
    [DataRow("<ProjectReference />", "Include")]
    [DataRow("<FrameworkReference />", "Include")]
    public async Task MissingRequiredReferenceAttributeReportsItsName(string reference, string attribute)
    {
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => ParseProject($"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>{reference}</ItemGroup>
            </Project>
            """));

        Assert.Contains(attribute, exception.Message);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MissingVersionDuringMutationDoesNotWriteTheFile(bool replacePackage)
    {
        var project = await ParseProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Example" Version="1.0.0" /></ItemGroup>
            </Project>
            """);
        var content = (await File.ReadAllTextAsync(project.PathOnDisk)).Replace(" Version=\"1.0.0\"", "");
        await File.WriteAllTextAsync(project.PathOnDisk, content);

        await Assert.ThrowsAsync<InvalidDataException>(() => replacePackage
            ? project.ReplacePackageReferenceAsync("Example", new Package("Replacement", "2.0.0"))
            : project.SetPackageReferenceVersionAsync("Example", new NugetVersion("2.0.0")));

        Assert.AreEqual(content, await File.ReadAllTextAsync(project.PathOnDisk));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NewItemGroupIsAddedUnderProjectAfterLeadingComment(bool packFile)
    {
        var project = await ParseProject("""
            <!-- Keep this project comment. -->
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        if (packFile)
            await project.PackFile("README.md");
        else
            await project.AddFrameworkReference("Microsoft.AspNetCore.App");

        var document = XDocument.Load(project.PathOnDisk);
        var entry = document.Root?.Element("ItemGroup")?.Element(packFile ? "None" : "FrameworkReference");
        Assert.IsNotNull(entry);
        Assert.AreEqual(packFile ? "README.md" : "Microsoft.AspNetCore.App", entry.Attribute("Include")?.Value);
    }
}
