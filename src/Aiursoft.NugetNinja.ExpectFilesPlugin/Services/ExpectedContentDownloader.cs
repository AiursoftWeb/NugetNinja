using Aiursoft.NugetNinja.Core.Services.Extractor;

namespace Aiursoft.NugetNinja.ExpectFilesPlugin.Services;

internal class ExpectedContentDownloader
{
    private readonly HttpClient _anonymous;
    private readonly HttpClient _authenticated;
    private readonly Dictionary<string, Dictionary<string, string>> _headers = new(StringComparer.OrdinalIgnoreCase);

    public ExpectedContentDownloader(HttpClient anonymous, HttpClient authenticated, NinjaConfig config)
    {
        _anonymous = anonymous;
        _authenticated = authenticated;
        if (config.HttpProfiles.Count != 0)
        {
            throw new InvalidOperationException("Move legacy httpProfiles selections to http.profiles in ninja.yaml.");
        }
        var profiles = config.Http?.Profiles ?? throw new InvalidOperationException("HTTP profiles must contain a mapping.");
        foreach (var definition in profiles.Values)
        {
            if (definition == null)
            {
                throw new InvalidOperationException("HTTP profiles must define an HTTPS origin and headersFromEnv.");
            }
            var origin = ParseOrigin(definition.Origin);
            if (_headers.ContainsKey(origin))
            {
                throw new InvalidOperationException("Multiple HTTP profiles match the same origin.");
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var request = new HttpRequestMessage(HttpMethod.Get, origin);
            foreach (var (header, variable) in definition.HeadersFromEnv ?? throw new InvalidOperationException("HTTP profile headers are missing."))
            {
                if (string.IsNullOrWhiteSpace(variable))
                {
                    throw new InvalidOperationException("HTTP profile headers must name a credential environment variable.");
                }
                // The repository cannot authorize a new destination for a runtime credential.
                var authorizedOrigin = Environment.GetEnvironmentVariable(variable + "_ORIGIN");
                if (string.IsNullOrWhiteSpace(authorizedOrigin) ||
                    !string.Equals(origin, ParseOrigin(authorizedOrigin), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("HTTP profile origin is not authorized by the credential's runtime origin binding.");
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
                throw new InvalidOperationException("An HTTP profile must define at least one header.");
            }
            _headers.Add(origin, headers);
        }
    }

    private static string ParseOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var origin) ||
            origin.Scheme != Uri.UriSchemeHttps || origin.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) ||
            !string.IsNullOrEmpty(origin.UserInfo))
        {
            throw new InvalidOperationException("HTTP credentials require an HTTPS origin without a path, query or credentials.");
        }
        return origin.GetLeftPart(UriPartial.Authority);
    }

    public async Task<string> GetStringAsync(string contentUri)
    {
        var uri = new Uri(contentUri, UriKind.Absolute);
        var origin = uri.GetLeftPart(UriPartial.Authority);
        if (!_headers.TryGetValue(origin, out var headers))
        {
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
}
