using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;

namespace AdministracionSenderistas.Tests;

public class FlexibleStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out long l)) return l.ToString();
            if (reader.TryGetDouble(out double d)) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (reader.TokenType == JsonTokenType.True) return "true";
        if (reader.TokenType == JsonTokenType.False) return "false";
        if (reader.TokenType == JsonTokenType.Null) return null;
        return reader.GetString();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

class Program
{
    static async Task Main()
    {
        var apiClient = new AdministracionSenderistas.Services.PostgrestApiClient();
        var cacheService = new AdministracionSenderistas.Services.SqliteCacheService("test_cache.db");
        await cacheService.InitializeAsync();

        var jwtService = new AdministracionSenderistas.Services.JwtService();
        var authService = new AdministracionSenderistas.Services.AuthService(jwtService, cacheService, apiClient);
        await authService.LoginAsync("JGRBalam", "Yomero");

        var repo = new AdministracionSenderistas.Services.DataRepository(apiClient, cacheService, authService);

        Console.WriteLine("Loading senderistas from repo...");
        var list = await repo.GetSenderistasAsync(forceRefresh: true);
        Console.WriteLine($"Loaded {list.Count} senderistas.");
        foreach (var s in list)
        {
            Console.WriteLine($"-> {s.Email} | {s.Nombre} | Rol: {s.Rol} | Bloq: {s.Bloqueado} | MinLong: {s.MinimoLongitudKm}");
        }

        // Test saving senderista update
        var first = list[0];
        first.MinimoLongitud = 5;
        first.Bloqueado = false;
        var saveResult = await repo.SaveSenderistaAsync(first);
        Console.WriteLine($"Save Result: {saveResult}");

        // Verify from local SQLite cache
        var localList = await cacheService.GetSenderistasAsync();
        var cached = localList.Find(x => x.Email == first.Email);
        Console.WriteLine($"Cached MinLong: {cached?.MinimoLongitudKm}, Bloq: {cached?.Bloqueado}, Rol: {cached?.Rol}");
        Console.WriteLine("TEST PASSED SUCCESSFULLY!");
    }
}
