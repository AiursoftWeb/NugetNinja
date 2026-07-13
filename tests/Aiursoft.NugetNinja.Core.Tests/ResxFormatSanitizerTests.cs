using Aiursoft.NugetNinja.Core.Services.Utils;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class ResxFormatSanitizerTests
{
    [TestMethod]
    public void EscapeInvalidFormatSpecifiers_EscapesNamedPlaceholdersOnly()
    {
        const string value = "API /api/sources/{id}; package {name}; count {0}; amount {1:N2}; escaped {{path}}";

        var result = ResxFormatSanitizer.EscapeInvalidFormatSpecifiers(value);

        Assert.AreEqual(
            "API /api/sources/{{id}}; package {{name}}; count {0}; amount {1:N2}; escaped {{path}}",
            result);
    }
}
