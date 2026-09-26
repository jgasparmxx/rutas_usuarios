using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace AdministracionSenderistas.Services;

public static class ImageLoaderService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly ConcurrentDictionary<string, Bitmap?> MemoryCache = new();

    public static async Task<Bitmap?> LoadImageAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url == "null")
            return null;

        if (MemoryCache.TryGetValue(url, out var cached))
            return cached;

        try
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                byte[] data = await HttpClient.GetByteArrayAsync(url);
                using var ms = new MemoryStream(data);
                var bmp = new Bitmap(ms);
                MemoryCache[url] = bmp;
                return bmp;
            }
            else if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                int commaIndex = url.IndexOf(',');
                if (commaIndex >= 0)
                {
                    string base64Data = url[(commaIndex + 1)..];
                    byte[] bytes = Convert.FromBase64String(base64Data);
                    using var ms = new MemoryStream(bytes);
                    var bmp = new Bitmap(ms);
                    MemoryCache[url] = bmp;
                    return bmp;
                }
            }
            else if (File.Exists(url))
            {
                using var fs = File.OpenRead(url);
                var bmp = new Bitmap(fs);
                MemoryCache[url] = bmp;
                return bmp;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageLoaderService] Error loading image from '{url}': {ex.Message}");
        }

        MemoryCache[url] = null;
        return null;
    }
}
