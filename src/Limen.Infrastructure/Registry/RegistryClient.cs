using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Limen.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Limen.Infrastructure.Registry;

public sealed class RegistryClient : IRegistryClient
{
    private readonly HttpClient _http;
    private readonly ILogger<RegistryClient> _log;

    private static readonly string[] AcceptHeaders =
    [
        "application/vnd.docker.distribution.manifest.v2+json",
        "application/vnd.oci.image.manifest.v1+json",
        "application/vnd.docker.distribution.manifest.list.v2+json",
        "application/vnd.oci.image.index.v1+json",
    ];

    public RegistryClient(HttpClient http, ILogger<RegistryClient> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<string?> GetManifestDigestAsync(string image, CancellationToken ct)
    {
        var (registry, repo, tag) = ParseImage(image);

        var url = $"https://{registry}/v2/{repo}/manifests/{tag}";

        try
        {
            var request = BuildHeadRequest(url);
            var response = await _http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var token = await GetBearerTokenAsync(response, ct);
                if (token is null)
                {
                    _log.LogWarning("Could not obtain bearer token for registry {Registry}", registry);
                    return null;
                }

                request = BuildHeadRequest(url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                response = await _http.SendAsync(request, ct);
            }

            if (response.IsSuccessStatusCode)
            {
                if (response.Headers.TryGetValues("Docker-Content-Digest", out var values))
                {
                    return values.FirstOrDefault();
                }

                _log.LogWarning("Registry returned success but no Docker-Content-Digest header for {Image}", image);
                return null;
            }

            _log.LogWarning("Registry returned {StatusCode} for {Image}", response.StatusCode, image);
            return null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to get manifest digest for {Image}", image);
            return null;
        }
    }

    private static HttpRequestMessage BuildHeadRequest(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Head, url);
        foreach (var accept in AcceptHeaders)
        {
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        }
        return req;
    }

    private async Task<string?> GetBearerTokenAsync(HttpResponseMessage unauthorizedResponse, CancellationToken ct)
    {
        var wwwAuth = unauthorizedResponse.Headers.WwwAuthenticate.FirstOrDefault();
        if (wwwAuth is null || wwwAuth.Scheme != "Bearer")
        {
            return null;
        }

        var param = wwwAuth.Parameter ?? string.Empty;
        var bearerParams = ParseBearerParams(param);
        bearerParams.TryGetValue("realm", out var realm);
        bearerParams.TryGetValue("service", out var service);
        bearerParams.TryGetValue("scope", out var scope);

        if (realm is null)
        {
            return null;
        }

        var tokenUrl = realm;
        var query = new List<string>();
        if (service is not null)
        {
            query.Add($"service={Uri.EscapeDataString(service)}");
        }

        if (scope is not null)
        {
            query.Add($"scope={Uri.EscapeDataString(scope)}");
        }

        if (query.Count > 0)
        {
            tokenUrl += "?" + string.Join("&", query);
        }

        try
        {
            var tokenResponse = await _http.GetAsync(tokenUrl, ct);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("token", out var tokenProp)
                ? tokenProp.GetString()
                : null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to fetch bearer token from {Realm}", realm);
            return null;
        }
    }

    /// <summary>
    /// Parses a Bearer WWW-Authenticate parameter string into a case-insensitive dictionary.
    /// Handles both quoted values (<c>realm="https://..."</c>) and unquoted tokens
    /// (<c>realm=https://...</c>), as allowed by RFC 7235.
    /// </summary>
    private static Dictionary<string, string> ParseBearerParams(string paramString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Split on commas that are not inside double quotes
        var tokens = SplitRespectingQuotes(paramString, ',');
        foreach (var token in tokens)
        {
            var chunk = token.Trim();
            var eqIdx = chunk.IndexOf('=');
            if (eqIdx <= 0)
            {
                continue;
            }

            var key = chunk[..eqIdx].Trim();
            var rawValue = chunk[(eqIdx + 1)..].Trim();

            // Strip surrounding double quotes if present
            string value;
            if (rawValue.Length >= 2 && rawValue[0] == '"' && rawValue[^1] == '"')
            {
                value = rawValue[1..^1];
            }
            else
            {
                value = rawValue;
            }

            result[key] = value;
        }

        return result;
    }

    private static IEnumerable<string> SplitRespectingQuotes(string input, char delimiter)
    {
        var start = 0;
        var inQuotes = false;
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (input[i] == delimiter && !inQuotes)
            {
                yield return input[start..i];
                start = i + 1;
            }
        }
        yield return input[start..];
    }

    private static (string registry, string repo, string tag) ParseImage(string image)
    {
        var registry = "registry-1.docker.io";
        var repo = image;
        var tag = "latest";

        var atIdx = image.IndexOf('@');
        if (atIdx >= 0)
        {
            tag = image[(atIdx + 1)..];
            repo = image[..atIdx];
        }
        else
        {
            var colonIdx = image.LastIndexOf(':');
            var slashIdx = image.LastIndexOf('/');
            if (colonIdx > slashIdx)
            {
                tag = image[(colonIdx + 1)..];
                repo = image[..colonIdx];
            }
        }

        var firstSlash = repo.IndexOf('/');
        if (firstSlash >= 0)
        {
            var possibleRegistry = repo[..firstSlash];
            if (possibleRegistry.Contains('.') || possibleRegistry.Contains(':') || possibleRegistry == "localhost")
            {
                registry = possibleRegistry;
                repo = repo[(firstSlash + 1)..];
            }
        }

        if (registry == "registry-1.docker.io" && !repo.Contains('/'))
        {
            repo = "library/" + repo;
        }

        return (registry, repo, tag);
    }
}
