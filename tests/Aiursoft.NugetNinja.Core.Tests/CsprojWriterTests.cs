using Aiursoft.NugetNinja.Core.Services.IO;
using HtmlAgilityPack;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class CsprojWriterTests
{
    /// <summary>
    /// Verifies that SortPropertyGroupChildren preserves newlines after &lt;PropertyGroup&gt;
    /// and before &lt;/PropertyGroup&gt; after reordering elements.
    /// </summary>
    [TestMethod]
    public async Task SaveCsprojToDisk_PropertyGroup_HasProperNewlinesAfterSort()
    {
        // Arrange: a PropertyGroup with elements in non-sorted order.
        var inputXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
";

        // Act
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(inputXml);

        var tmpFile = Path.GetTempFileName();
        try
        {
            await new CsprojWriter().SaveCsprojToDisk(doc, tmpFile);
            var result = await File.ReadAllTextAsync(tmpFile);

            Console.WriteLine("Output:");
            Console.WriteLine(result);

            // Assert: PropertyGroup opening tag should be followed by a newline
            Assert.IsTrue(result.Contains(">\n    <"),
                "PropertyGroup opening tag should be followed by newline+indent before first element.");

            // Assert: PropertyGroup closing tag should be preceded by a newline
            Assert.IsTrue(result.Contains(">\n  </PropertyGroup>"),
                "Last element should be followed by newline before </PropertyGroup>.");

            // Assert: elements are sorted (OutputType before TargetFramework before Nullable, per PropertyOrder)
            var outputTypeIndex = result.IndexOf("<OutputType>", StringComparison.Ordinal);
            var targetFrameworkIndex = result.IndexOf("<TargetFramework>", StringComparison.Ordinal);
            var nullableIndex = result.IndexOf("<Nullable>", StringComparison.Ordinal);
            Assert.IsTrue(outputTypeIndex < targetFrameworkIndex, "OutputType should come before TargetFramework.");
            Assert.IsTrue(targetFrameworkIndex < nullableIndex, "TargetFramework should come before Nullable.");

            // Assert: no malformed concatenation like <tag><tag>
            Assert.IsFalse(result.Contains("</OutputType><"), "No tag should be immediately followed by another opening tag.");
            Assert.IsFalse(result.Contains("</TargetFramework><"), "No tag should be immediately followed by another opening tag.");
            Assert.IsFalse(result.Contains("></PropertyGroup>"), "Should not have empty PropertyGroup pattern.");
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    /// <summary>
    /// Verifies that AssemblyAttribute with _Parameter1 children is NOT corrupted.
    /// AssemblyAttribute is no longer in the SelfClosingElements list because it can
    /// have child elements.
    /// </summary>
    [TestMethod]
    public async Task SaveCsprojToDisk_AssemblyAttribute_WithChildElements_IsPreserved()
    {
        // Arrange: a csproj with AssemblyAttribute containing _Parameter1 children.
        var inputXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <AssemblyAttribute Include=""System.Runtime.CompilerServices.InternalsVisibleTo"">
      <_Parameter1>Aiursoft.Apkg.WebTests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
";

        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(inputXml);

        var tmpFile = Path.GetTempFileName();
        try
        {
            await new CsprojWriter().SaveCsprojToDisk(doc, tmpFile);
            var result = await File.ReadAllTextAsync(tmpFile);

            Console.WriteLine("Output:");
            Console.WriteLine(result);

            // Assert: _Parameter1 has its closing tag
            Assert.IsTrue(result.Contains("</_Parameter1>"),
                "_Parameter1 closing tag must be present.");

            // Assert: AssemblyAttribute is NOT self-closed (it has children)
            Assert.IsTrue(result.Contains("</AssemblyAttribute>"),
                "AssemblyAttribute should have a closing tag when it contains children.");

            // Assert: _Parameter1 is still inside AssemblyAttribute
            var paramEnd = result.IndexOf("</_Parameter1>", StringComparison.Ordinal);
            var attribEnd = result.IndexOf("</AssemblyAttribute>", StringComparison.Ordinal);
            Assert.IsTrue(paramEnd < attribEnd,
                "_Parameter1 closing tag should be before AssemblyAttribute closing tag.");
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    /// <summary>
    /// Verifies that AssemblyAttribute with multiple _Parameter1 children survives
    /// the save round-trip intact.
    /// </summary>
    [TestMethod]
    public async Task SaveCsprojToDisk_AssemblyAttribute_MultipleChildElements_AllPreserved()
    {
        // Arrange: a csproj with AssemblyAttribute containing two _Parameter1 children.
        var inputXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <AssemblyAttribute Include=""System.Runtime.CompilerServices.InternalsVisibleTo"">
      <_Parameter1>Aiursoft.Apkg.WebTests</_Parameter1>
      <_Parameter1>Aiursoft.Apkg</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
";

        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(inputXml);

        var tmpFile = Path.GetTempFileName();
        try
        {
            await new CsprojWriter().SaveCsprojToDisk(doc, tmpFile);
            var result = await File.ReadAllTextAsync(tmpFile);

            Console.WriteLine("Output:");
            Console.WriteLine(result);

            // Assert: both _Parameter1 tags have closing tags
            var closeCount = CountSubstring(result, "</_Parameter1>");
            Assert.AreEqual(2, closeCount,
                "Both _Parameter1 elements should have their closing tags.");

            // Assert: the text content is preserved (whitespace between text and closing tag is expected)
            Assert.IsTrue(result.Contains("Aiursoft.Apkg.WebTests"),
                "First _Parameter1 text content should be preserved.");
            Assert.IsTrue(result.Contains("Aiursoft.Apkg"),
                "Second _Parameter1 text content should be preserved.");

            // Assert: AssemblyAttribute still properly closed
            Assert.IsTrue(result.Contains("</AssemblyAttribute>"),
                "AssemblyAttribute closing tag must be present.");
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    /// <summary>
    /// Verifies that property update operations (which trigger SaveCsprojToDisk)
    /// do not corrupt the PropertyGroup structure.
    /// </summary>
    [TestMethod]
    public async Task SaveCsprojToDisk_EmptyPropertyGroup_DoesNotCorrupt()
    {
        // Arrange: a csproj with an empty PropertyGroup.
        var inputXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
  </PropertyGroup>
</Project>
";

        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(inputXml);

        var tmpFile = Path.GetTempFileName();
        try
        {
            await new CsprojWriter().SaveCsprojToDisk(doc, tmpFile);
            var result = await File.ReadAllTextAsync(tmpFile);

            Console.WriteLine("Output:");
            Console.WriteLine(result);

            // Assert: the document is still well-formed (can be parsed back)
            var reDoc = new HtmlDocument
            {
                OptionOutputOriginalCase = true,
                OptionAutoCloseOnEnd = true,
                OptionWriteEmptyNodes = true
            };
            reDoc.LoadHtml(result);
            var propertyGroup = reDoc.DocumentNode.Descendants("PropertyGroup").FirstOrDefault();
            Assert.IsNotNull(propertyGroup, "PropertyGroup should still exist after save.");
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    /// <summary>
    /// Verifies that multiple PropertyGroup elements in the same file are all sorted correctly
    /// without cross-contamination.
    /// </summary>
    [TestMethod]
    public async Task SaveCsprojToDisk_MultiplePropertyGroups_AllSorted()
    {
        // Arrange
        var inputXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <PropertyGroup>
    <Nullable>disable</Nullable>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
";

        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(inputXml);

        var tmpFile = Path.GetTempFileName();
        try
        {
            await new CsprojWriter().SaveCsprojToDisk(doc, tmpFile);
            var result = await File.ReadAllTextAsync(tmpFile);

            Console.WriteLine("Output:");
            Console.WriteLine(result);

            // Assert: both PropertyGroups have proper newlines after opening tag
            var pgOpenings = CountSubstring(result, "<PropertyGroup>\n");
            Assert.IsTrue(pgOpenings >= 2,
                "Each PropertyGroup opening should be followed by a newline.");

            // Assert: both PropertyGroups have proper newlines before closing tag
            var pgClosings = CountSubstring(result, "\n  </PropertyGroup>");
            Assert.IsTrue(pgClosings >= 2,
                "Each PropertyGroup closing should be preceded by a newline.");
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    /// <summary>
    /// Verifies the exact bug scenario: no element should be concatenated
    /// directly after a &lt;PropertyGroup&gt; opening tag without a newline.
    /// </summary>
    [TestMethod]
    public async Task SaveCsprojToDisk_ShouldNotHaveConcatenatedElements()
    {
        var inputXml = @"<Project Sdk=""Microsoft.NET.Sdk.Web"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
";

        var result = await SaveAndReadBack(inputXml);

        Console.WriteLine("Output:");
        Console.WriteLine(result);

        // Key regression: must NOT have "<PropertyGroup><" pattern
        Assert.IsFalse(result.Contains("<PropertyGroup><"),
            "Elements should not be concatenated with PropertyGroup opening tag.");
    }

    /// <summary>
    /// Verifies that a nested PropertyGroup (e.g., conditional) is preserved
    /// as a child and not flattened into the parent PropertyGroup.
    /// </summary>
    [TestMethod]
    public async Task Sort_ShouldNotFlattenNestedPropertyGroups()
    {
        var inputXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
";
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(inputXml);

        // Add a nested PropertyGroup as a child of the main PropertyGroup
        // (simulating a conditional property group scenario)
        var mainPg = doc.DocumentNode.Descendants("PropertyGroup").First();
        var nestedPg = doc.CreateElement("PropertyGroup");
        nestedPg.Attributes.Add("Condition", "'$(Configuration)' == 'Debug'");
        var nestedProp = doc.CreateElement("DefineConstants");
        nestedProp.InnerHtml = "DEBUG;TRACE";
        nestedPg.AppendChild(nestedProp);
        mainPg.AppendChild(nestedPg);

        var tmpFile = Path.GetTempFileName();
        try
        {
            await new CsprojWriter().SaveCsprojToDisk(doc, tmpFile);
            var result = await File.ReadAllTextAsync(tmpFile);

            Console.WriteLine("Output:");
            Console.WriteLine(result);

            // The nested PropertyGroup should be preserved, not flattened
            Assert.IsTrue(result.Contains("DefineConstants"),
                "Nested PropertyGroup should be preserved as a child, not flattened.");
            Assert.IsTrue(result.Contains("Condition"),
                "Nested PropertyGroup's Condition attribute should be preserved.");
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    /// <summary>
    /// Verifies that ItemGroup elements like PackageReference are preserved
    /// with self-closing syntax after the round-trip.
    /// </summary>
    [TestMethod]
    public async Task SaveCsprojToDisk_ShouldPreserveItemGroupElements()
    {
        var inputXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />
  </ItemGroup>
</Project>
";

        var result = await SaveAndReadBack(inputXml);

        Console.WriteLine("Output:");
        Console.WriteLine(result);

        // PackageReference should use self-closing tag
        Assert.IsTrue(result.Contains("Newtonsoft.Json") && result.Contains(" />"),
            "PackageReference should be self-closing.");
        Assert.IsTrue(result.Contains("<ItemGroup>"),
            "ItemGroup should be preserved.");
    }

    /// <summary>
    /// Verifies that the _Parameter1 regex fix does not capture trailing whitespace
    /// inside the text content of underscore-prefixed elements.
    /// </summary>
    [TestMethod]
    public async Task SaveCsprojToDisk_Parameter1_NoWhitespaceCorruption()
    {
        var inputXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <AssemblyAttribute Include=""System.Runtime.CompilerServices.InternalsVisibleTo"">
      <_Parameter1>Aiursoft.Apkg.WebTests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
";

        var result = await SaveAndReadBack(inputXml);

        Console.WriteLine("Output:");
        Console.WriteLine(result);

        // _Parameter1 must have its closing tag right after the content,
        // not with extra whitespace/newlines in between.
        Assert.IsTrue(result.Contains(">Aiursoft.Apkg.WebTests</_Parameter1>"),
            "_Parameter1 content should not have extra whitespace before closing tag.");
    }

    /// <summary>
    /// Helper to load, save, and read back a csproj string.
    /// </summary>
    private static async Task<string> SaveAndReadBack(string csprojContent)
    {
        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);

        var tempFile = Path.GetTempFileName();
        try
        {
            await new CsprojWriter().SaveCsprojToDisk(doc, tempFile);
            return await File.ReadAllTextAsync(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static int CountSubstring(string text, string substring)
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
