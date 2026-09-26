using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdministracionSenderistas.Services;

public class PostgrestApiClient : IPostgrestApiClient
{
    private readonly HttpClient _httpClient;
    public string BaseUrl { get; } = "https://postgrest-latest-09dh.onrender.com";

    // Flag indicating whether the remote PostgREST server has PGRST_JWT_SECRET configured
    private static bool _serverLacksJwtSecret = false;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        opts.Converters.Add(new FlexibleStringConverter());
        return opts;
    }

    public PostgrestApiClient(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            _httpClient = new HttpClient(handler);
        }
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync("/", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<T?> GetAsync<T>(string endpointAndQuery, string token)
    {
        var response = await SendWithJwtFallbackAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Get, endpointAndQuery);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return req;
        }, token);

        await EnsureSuccessWithDetailsAsync(response);

        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }

    public async Task<T?> PostAsync<T>(string endpoint, object data, string token, bool upsert = true)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);

        var response = await SendWithJwtFallbackAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (upsert)
            {
                req.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
            }
            else
            {
                req.Headers.Add("Prefer", "return=representation");
            }
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return req;
        }, token);

        await EnsureSuccessWithDetailsAsync(response);

        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }

    public async Task<bool> PatchAsync(string endpointAndQuery, object data, string token)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);

        var response = await SendWithJwtFallbackAsync(() =>
        {
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), endpointAndQuery);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.Add("Prefer", "return=representation");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return req;
        }, token);

        if (!response.IsSuccessStatusCode)
        {
            string err = string.Empty;
            try { err = await response.Content.ReadAsStringAsync(); } catch { }
            Console.WriteLine($"[PostgrestApiClient] PATCH {endpointAndQuery} failed with {(int)response.StatusCode} ({response.ReasonPhrase}): {err}");
            return false;
        }

        string respBody = string.Empty;
        try { respBody = await response.Content.ReadAsStringAsync(); } catch { }
        if (respBody.Trim() == "[]")
        {
            Console.WriteLine($"[PostgrestApiClient] PATCH {endpointAndQuery} matched 0 rows.");
            return false;
        }

        return true;
    }

    public async Task<bool> DeleteAsync(string endpointAndQuery, string token)
    {
        var response = await SendWithJwtFallbackAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Delete, endpointAndQuery);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return req;
        }, token);

        return response.IsSuccessStatusCode;
    }

    private async Task<HttpResponseMessage> SendWithJwtFallbackAsync(Func<HttpRequestMessage> createRequest, string token)
    {
        bool useToken = !string.IsNullOrWhiteSpace(token) && !_serverLacksJwtSecret;

        using var initialRequest = createRequest();
        if (useToken)
        {
            initialRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await _httpClient.SendAsync(initialRequest);

        // Check if the server returned 500 due to PGRST300 (Server lacks JWT secret in Render)
        if (useToken && (int)response.StatusCode == 500)
        {
            string errorBody = await response.Content.ReadAsStringAsync();
            if (errorBody.Contains("PGRST300") || errorBody.Contains("Server lacks JWT secret"))
            {
                // Remote PostgREST has not configured PGRST_JWT_SECRET yet.
                // Fall back to querying without Bearer token (PostgREST assumes anon role 'senderista').
                _serverLacksJwtSecret = true;

                using var retryRequest = createRequest();
                return await _httpClient.SendAsync(retryRequest);
            }
        }

        return response;
    }

    private static async Task EnsureSuccessWithDetailsAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            string body = string.Empty;
            try
            {
                body = await response.Content.ReadAsStringAsync();
            }
            catch { }

            string msg = $"Error HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
            if (!string.IsNullOrWhiteSpace(body))
            {
                msg += $": {body}";
            }
            throw new HttpRequestException(msg, null, response.StatusCode);
        }
    }
}
