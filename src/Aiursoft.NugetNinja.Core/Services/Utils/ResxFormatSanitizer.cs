using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Aiursoft.NugetNinja.Core.Services.Utils;

public static class ResxFormatSanitizer
{
    private static readonly Regex InvalidFormatSpecifier = new(
        @"(?<!\{)\{[A-Za-z_][^}]*\}",
        RegexOptions.Compiled);

    public static string EscapeInvalidFormatSpecifiers(string value)
    {
        return InvalidFormatSpecifier.Replace(value, match => $"{{{match.Value}}}");
    }

    public static async Task<int> EscapeInvalidFormatSpecifiersAsync(string path, CancellationToken cancellationToken)
    {
        var updatedFiles = 0;
        foreach (var resxFile in Directory.EnumerateFiles(path, "*.resx", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = XDocument.Load(resxFile, LoadOptions.PreserveWhitespace);
            var changed = false;

            foreach (var value in document.Descendants("value"))
            {
                var escapedValue = EscapeInvalidFormatSpecifiers(value.Value);
                if (escapedValue == value.Value)
                    continue;

                value.Value = escapedValue;
                changed = true;
            }

            if (!changed)
                continue;

            await using var stream = File.Create(resxFile);
            await document.SaveAsync(stream, SaveOptions.DisableFormatting, cancellationToken);
            updatedFiles++;
        }

        return updatedFiles;
    }
}
