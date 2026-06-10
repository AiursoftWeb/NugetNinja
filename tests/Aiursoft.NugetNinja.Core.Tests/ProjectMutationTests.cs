using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.IO;
using HtmlAgilityPack;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class ProjectMutationTests
{
    private string CreateTempCsproj(string content)
    {
        var path = Path.GetTempFileName();
        // Delete the 0-byte file and create our own .csproj
        File.Delete(path);
        path = Path.ChangeExtension(path, ".csproj");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Creates a Project from a csproj string by writing to a temp file and parsing it.
    /// </summary>
    private async Task<Project> CreateProjectAsync(string csprojContent)
    {
        var path = CreateTempCsproj(csprojContent);
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);
        return new Project(path, doc.DocumentNode);
    }

    // ── SetPackageReferenceVersionAsync ────────────────────────────

    [TestMethod]
    public async Task SetPackageReferenceVersionAsync_UpdatesExitingVersion()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />
  </ItemGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.SetPackageReferenceVersionAsync("Newtonsoft.Json", new NugetVersion("14.0.0"));

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        Assert.IsTrue(result.Contains("14.0.0"), "Version should be updated to 14.0.0.");
        Assert.IsFalse(result.Contains("13.0.3"), "Old version should no longer appear.");
        Assert.IsTrue(result.Contains("</Project>"), "Document structure should be intact.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task SetPackageReferenceVersionAsync_ThrowsWhenNotFound()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />
  </ItemGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        try
        {
            var threw = false;
            try
            {
                await project.SetPackageReferenceVersionAsync("NoSuchPackage", new NugetVersion("1.0.0"));
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Should throw InvalidOperationException when package is not found.");
        }
        finally
        {
            Cleanup(project.PathOnDisk);
        }
    }

    // ── ReplacePackageReferenceAsync ────────────────────────────────

    [TestMethod]
    public async Task ReplacePackageReferenceAsync_ReplacesBothIncludeAndVersion()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Old.Package"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.ReplacePackageReferenceAsync("Old.Package",
            new Package("New.Package", new NugetVersion("2.0.0")));

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        Assert.IsTrue(result.Contains("New.Package"), "Include should be changed to new package name.");
        Assert.IsTrue(result.Contains("2.0.0"), "Version should be changed to new version.");
        Assert.IsFalse(result.Contains("Old.Package"), "Old package name should be gone.");
        Assert.IsFalse(result.Contains("1.0.0"), "Old version should be gone.");

        Cleanup(project.PathOnDisk);
    }

    // ── RemovePackageReferenceAsync ─────────────────────────────────

    [TestMethod]
    public async Task RemovePackageReferenceAsync_RemovesPackageAndEmptyItemGroup()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Only.Package"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.RemovePackageReferenceAsync("Only.Package");

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        Assert.IsFalse(result.Contains("Only.Package"), "Package should be removed.");
        Assert.IsFalse(result.Contains("PackageReference"), "No PackageReference should remain.");
        // The now-empty ItemGroup should also be removed
        Assert.IsFalse(result.Contains("ItemGroup"), "Empty ItemGroup should be removed.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task RemovePackageReferenceAsync_KeepsItemGroupWhenOtherPackagesRemain()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Keep.Me"" Version=""2.0.0"" />
    <PackageReference Include=""Remove.Me"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.RemovePackageReferenceAsync("Remove.Me");

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        Assert.IsTrue(result.Contains("Keep.Me"), "Other package should remain.");
        Assert.IsFalse(result.Contains("Remove.Me"), "Removed package should be gone.");
        Assert.IsTrue(result.Contains("</ItemGroup>"), "ItemGroup should still exist.");

        Cleanup(project.PathOnDisk);
    }

    // ── RemoveProjectReference ──────────────────────────────────────

    [TestMethod]
    public async Task RemoveProjectReference_RemovesProjectReference()
    {
        var refDir = Path.Combine(Path.GetTempPath(), "RefProject");
        Directory.CreateDirectory(refDir);
        var refPath = Path.Combine(refDir, "Ref.csproj");
        File.WriteAllText(refPath, @"<Project Sdk=""Microsoft.NET.Sdk"" />");
        try
        {
            // Create the main project with relative path
            var mainDir = Path.Combine(Path.GetTempPath(), "MainProject");
            Directory.CreateDirectory(mainDir);
            var content = @$"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\RefProject\Ref.csproj"" />
  </ItemGroup>
</Project>";
            var mainPath = Path.Combine(mainDir, "Main.csproj");
            File.WriteAllText(mainPath, content);

            var doc = new HtmlDocument
            {
                OptionOutputOriginalCase = true,
                OptionAutoCloseOnEnd = true,
                OptionWriteEmptyNodes = true
            };
            doc.LoadHtml(content);
            var project = new Project(mainPath, doc.DocumentNode);
            await project.RemoveProjectReference(refPath);

            var result = await File.ReadAllTextAsync(mainPath);
            Assert.IsFalse(result.Contains("ProjectReference"), "ProjectReference should be removed.");
            Assert.IsFalse(result.Contains("ItemGroup"), "Empty ItemGroup should be removed.");

            Cleanup(mainPath);
            Cleanup(refPath);
        }
        finally
        {
            if (File.Exists(refPath)) File.Delete(refPath);
            Directory.Delete(refDir, true);
        }
    }

    // ── AddOrUpdateProperty ─────────────────────────────────────────

    [TestMethod]
    public async Task AddOrUpdateProperty_UpdatesExistingProperty()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.AddOrUpdateProperty("TargetFramework", "net10.0");

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        Assert.IsTrue(result.Contains("net10.0"), "TargetFramework should be updated.");
        Assert.IsFalse(result.Contains("net8.0"), "Old value should be gone.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task AddOrUpdateProperty_AddsNewProperty()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.AddOrUpdateProperty("Nullable", "enable");

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        Assert.IsTrue(result.Contains("<Nullable>enable</Nullable>"), "Nullable should be added.");
        Assert.IsTrue(result.Contains("</PropertyGroup>"), "PropertyGroup should be intact.");

        Cleanup(project.PathOnDisk);
    }

    // ── DeduplicateProperty ─────────────────────────────────────────

    [TestMethod]
    public async Task DeduplicateProperty_RemovesDuplicatesAndKeepsFirst()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.DeduplicateProperty("Nullable", "disable");

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        var nullableCount = CountOccurrences(result, "<Nullable>");
        Assert.AreEqual(1, nullableCount, "Should have exactly one Nullable element after deduplication.");
        Assert.IsTrue(result.Contains("<Nullable>disable</Nullable>"), "The first value should be preserved.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task DeduplicateProperty_NoDuplicates_DoesNothing()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.DeduplicateProperty("Nullable", "enable");

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        var nullableCount = CountOccurrences(result, "<Nullable>");
        Assert.AreEqual(1, nullableCount, "No-op when no duplicates.");

        Cleanup(project.PathOnDisk);
    }

    // ── RemoveProperty ──────────────────────────────────────────────

    [TestMethod]
    public async Task RemoveProperty_RemovesAllOccurrences()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <SomeProperty>value1</SomeProperty>
    <SomeProperty>value2</SomeProperty>
  </PropertyGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.RemoveProperty("SomeProperty");

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        Assert.IsFalse(result.Contains("SomeProperty"), "All occurrences should be removed.");
        Assert.IsTrue(result.Contains("TargetFramework"), "Other properties should remain.");

        Cleanup(project.PathOnDisk);
    }

    // ── AddFrameworkReference ───────────────────────────────────────

    [TestMethod]
    public async Task AddFrameworkReference_AddsToExistingItemGroup()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Some.Package"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.AddFrameworkReference("Microsoft.AspNetCore.App");

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        Assert.IsTrue(result.Contains(@"<FrameworkReference Include=""Microsoft.AspNetCore.App"" />"),
            "FrameworkReference should be added.");
        Assert.IsTrue(result.Contains("Some.Package"), "Existing package reference should remain.");

        Cleanup(project.PathOnDisk);
    }

    // ── PackFile ────────────────────────────────────────────────────

    [TestMethod]
    public async Task PackFile_AddsNoneElement()
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Some.Package"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
        var project = await CreateProjectAsync(content);
        await project.PackFile("README.md");

        var result = await File.ReadAllTextAsync(project.PathOnDisk);
        Assert.IsTrue(result.Contains(@"<None Include=""README.md"" Pack=""true"" PackagePath=""."""),
            "None element should be added.");

        Cleanup(project.PathOnDisk);
    }

    // ── Round-trip integrity ────────────────────────────────────────

    [TestMethod]
    public async Task MutationRoundTrip_NoDataLoss()
    {
        // Simulate a realistic multi-step mutation session
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Outdated"" Version=""1.0.0"" />
    <PackageReference Include=""Keep"" Version=""2.0.0"" />
    <ProjectReference Include=""..\Other\Other.csproj"" />
  </ItemGroup>
</Project>";
        var project = await CreateProjectAsync(content);

        // Step 1: Update a package version
        await project.SetPackageReferenceVersionAsync("Keep", new NugetVersion("3.0.0"));

        // Step 2: Add a property
        await project.AddOrUpdateProperty("IsPackable", "false");

        // Step 3: Remove a package
        await project.RemovePackageReferenceAsync("Outdated");

        var result = await File.ReadAllTextAsync(project.PathOnDisk);

        // All expected content should be present
        Assert.IsTrue(result.Contains("3.0.0"), "Keep package version should be updated.");
        Assert.IsTrue(result.Contains("<IsPackable>false</IsPackable>"), "New property should be added.");
        Assert.IsFalse(result.Contains("Outdated"), "Outdated package should be removed.");
        Assert.IsTrue(result.Contains("OutputType"), "Original properties should survive.");
        Assert.IsTrue(result.Contains("</PropertyGroup>"), "PropertyGroup should be well-formed.");
        Assert.IsTrue(result.Contains("</Project>"), "Project tag should be intact.");

        // Verify XML is still parseable
        var reDoc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        reDoc.LoadHtml(result);
        Assert.IsNotNull(reDoc.DocumentNode.Descendants("PropertyGroup").FirstOrDefault(),
            "Output should be parseable XML with PropertyGroup.");

        Cleanup(project.PathOnDisk);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static void Cleanup(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static int CountOccurrences(string text, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }
}
