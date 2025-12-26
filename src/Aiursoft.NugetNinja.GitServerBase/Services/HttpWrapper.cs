using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.GitServerBase.Services;

public class HttpWrapper(
    ILogger<HttpWrapper> logger,
    HttpClient httpClient)
{
    public async Task<string> SendHttp(string endPoint, HttpMethod method, string patToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, endPoint)
        {
            Content = body != null
                ? JsonContent.Create(body)
                : null
        };

        request.Headers.Add("Authorization", $"Bearer {patToken}");
        request.Headers.Add("User-Agent", $"curl/7.76.1");
        request.Headers.Add("accept", "application/json");

        logger.LogTrace("{Method}: to endpoint: {EndPoint}...", method.Method, endPoint);

        var response = await httpClient.SendAsync(request);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            var error = await response.Content.ReadAsStringAsync();
            logger.LogWarning("Endpoint: {EndPoint} respond error: {Error}", endPoint, error);
            throw;
        }

        var json = await response.Content.ReadAsStringAsync();
        return json;
    }

    public async Task<T> SendHttpAndGetJson<T>(string endPoint, HttpMethod method, string patToken)
    {
        var json = await SendHttp(endPoint, method, patToken);
        var repos = JsonSerializer.Deserialize<T>(json) ??
                    throw new InvalidOperationException($"The remote server returned non-json content: '{json}'");
        return repos;
    }
}
