using System.Text.Json;

namespace Aiursoft.NugetNinja.ExpectFilesPlugin.Services;

internal class ExpectedContentDownloader
{
    private readonly HttpClient _anonymous;
    private readonly HttpClient _authenticated;
    private readonly Dictionary<string, Dictionary<string, string>> _headers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _protectedOrigins = new(StringComparer.OrdinalIgnoreCase);

    public ExpectedContentDownloader(HttpClient anonymous, HttpClient authenticated, IEnumerable<string> selectedProfiles)
    {
        _anonymous = anonymous;
        _authenticated = authenticated;
        var json = Environment.GetEnvironmentVariable("NINJA_HTTP_PROFILES");
        Dictionary<string, Profile?> definitions;
        try
        {
            definitions = string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, Profile?>>(json,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                  ?? throw new InvalidOperationException("NINJA_HTTP_PROFILES must contain an object.");
        }
        catch (JsonException)
        {
            // Parser exceptions can include configuration values. Do not expose them.
            throw new InvalidOperationException("NINJA_HTTP_PROFILES contains invalid JSON.");
        }

        foreach (var definition in definitions.Values)
        {
            if (definition == null || !Uri.TryCreate(definition.Origin, UriKind.Absolute, out var origin) ||
                origin.Scheme != Uri.UriSchemeHttps || origin.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) ||
                !string.IsNullOrEmpty(origin.UserInfo))
            {
                throw new InvalidOperationException("HTTP profiles require an HTTPS origin without a path, query or credentials.");
            }
            _protectedOrigins.Add(origin.GetLeftPart(UriPartial.Authority));
        }

        foreach (var name in selectedProfiles.Distinct(StringComparer.Ordinal))
        {
            if (!definitions.TryGetValue(name, out var definition) || definition == null)
            {
                throw new InvalidOperationException($"HTTP profile '{name}' is not configured in the runtime environment.");
            }
            var origin = new Uri(definition.Origin).GetLeftPart(UriPartial.Authority);
            if (_headers.ContainsKey(origin))
            {
                throw new InvalidOperationException("Multiple selected HTTP profiles match the same origin.");
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var request = new HttpRequestMessage(HttpMethod.Get, origin);
            foreach (var (header, variable) in definition.HeadersFromEnv ?? throw new InvalidOperationException("HTTP profile headers are missing."))
            {
                if (string.IsNullOrWhiteSpace(variable))
                {
                    throw new InvalidOperationException("HTTP profile headers must name a credential environment variable.");
                }
                var value = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException($"Credential environment variable '{variable}' is missing or empty.");
                }
                if (value.Contains('\r') || value.Contains('\n') ||
                    !request.Headers.TryAddWithoutValidation(header, value))
                {
                    throw new InvalidOperationException("HTTP profile contains an invalid request header.");
                }
                headers.Add(header, value);
            }
            if (headers.Count == 0)
            {
                throw new InvalidOperationException("A selected HTTP profile must define at least one header.");
            }
            _headers.Add(origin, headers);
        }
    }

    public async Task<string> GetStringAsync(string contentUri)
    {
        var uri = new Uri(contentUri, UriKind.Absolute);
        var origin = uri.GetLeftPart(UriPartial.Authority);
        if (!_headers.TryGetValue(origin, out var headers))
        {
            if (_protectedOrigins.Contains(origin))
            {
                throw new InvalidOperationException("This origin requires an HTTP profile selected in ninja.yaml.");
            }
            return await _anonymous.GetStringAsync(uri);
        }
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("Authenticated content URIs must not contain credentials.");
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        foreach (var (header, value) in headers)
        {
            request.Headers.Add(header, value);
        }
        using var response = await _authenticated.SendAsync(request);
        // This client does not follow redirects. A sign-in page is not expected file content.
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private class Profile
    {
        public string Origin { get; set; } = string.Empty;
        public Dictionary<string, string> HeadersFromEnv { get; set; } = [];
    }
}
