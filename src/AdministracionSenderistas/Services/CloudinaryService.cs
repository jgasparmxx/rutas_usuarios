using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdministracionSenderistas.Services;

public class CloudinaryService : ICloudinaryService
{
    public const string DefaultCloudName = "nt7ex7ob";
    public const string DefaultApiKey = "473617795418392";
    public const string DefaultApiSecret = "yaig42wX1gLF-3t-lv1l2cq5MPA";

    private readonly string _cloudName;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly HttpClient _httpClient;

    public CloudinaryService(
        string cloudName = DefaultCloudName,
        string apiKey = DefaultApiKey,
        string apiSecret = DefaultApiSecret,
        HttpClient? httpClient = null)
    {
        _cloudName = cloudName;
        _apiKey = apiKey;
        _apiSecret = apiSecret;

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
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }
    }

    public async Task<string?> UploadImageAsync(byte[] imageBytes, string publicId, string folder = "rutas_senderismo")
    {
        if (imageBytes == null || imageBytes.Length == 0) return null;

        try
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string overwrite = "true";

            // Cloudinary requires signing parameters alphabetically: folder, overwrite, public_id, timestamp
            string toSign = $"folder={folder}&overwrite={overwrite}&public_id={publicId}&timestamp={timestamp}{_apiSecret}";
            byte[] hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(toSign));
            string signature = Convert.ToHexString(hashBytes).ToLowerInvariant();

            using var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(imageBytes);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            content.Add(byteContent, "file", $"{publicId}.jpg");
            content.Add(new StringContent(_apiKey), "api_key");
            content.Add(new StringContent(timestamp.ToString()), "timestamp");
            content.Add(new StringContent(folder), "folder");
            content.Add(new StringContent(publicId), "public_id");
            content.Add(new StringContent(overwrite), "overwrite");
            content.Add(new StringContent(signature), "signature");

            string uploadUrl = $"https://api.cloudinary.com/v1_1/{_cloudName}/image/upload";
            var response = await _httpClient.PostAsync(uploadUrl, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("secure_url", out var secUrl))
                {
                    return secUrl.GetString();
                }
            }
            else
            {
                Console.WriteLine($"[CloudinaryService] Upload error: {response.StatusCode} - {responseBody}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CloudinaryService] Upload exception: {ex.Message}");
        }

        return null;
    }
}
