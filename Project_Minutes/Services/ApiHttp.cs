using System.Net.Http.Headers;
using Project_Minutes.Configuration;

namespace Project_Minutes.Services;

/// <summary>Cliente HTTP compartido hacia la API REST (backend).</summary>
public static class ApiHttp
{
    private static HttpClient? _client;

    public static HttpClient Instance => _client ??= Create();

    public static void ResetForTests() => _client = null;

    private static HttpClient Create()
    {
        var cfg = ClientConfiguration.Load();
        var baseUri = cfg.ApiBaseUrl.TrimEnd('/') + "/";
        var c = new HttpClient { BaseAddress = new Uri(baseUri) };
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return c;
    }
}
