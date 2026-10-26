using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdministracionSenderistas.Services;

/// <summary>
/// Convertidor JSON bidireccional para el tipo BYTEA de PostgreSQL / PostgREST.
/// PostgREST serializa los campos BYTEA como texto hexadecimal con prefijo "\x" (ej: "\xffd8ffe0...").
/// Al escribir hacia PostgREST, también se envía como "\x..." en minúsculas.
/// Como alternativa de lectura, soporta Base64 estándar si viniera en ese formato.
/// </summary>
public class PostgresByteaJsonConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str))
            {
                return null;
            }

            // Formato hexadecimal de PostgreSQL: "\x..." o "\\x..."
            if (str.StartsWith("\\x", StringComparison.OrdinalIgnoreCase))
            {
                string hex = str.Substring(2);
                try
                {
                    return Convert.FromHexString(hex);
                }
                catch
                {
                    return null;
                }
            }

            if (str.StartsWith(@"\x", StringComparison.OrdinalIgnoreCase))
            {
                string hex = str.Substring(2);
                try
                {
                    return Convert.FromHexString(hex);
                }
                catch
                {
                    return null;
                }
            }

            // Fallback 1: Base64 convencional
            try
            {
                return Convert.FromBase64String(str);
            }
            catch
            {
                // Fallback 2: Cadena hexadecimal sin prefijo
                try
                {
                    return Convert.FromHexString(str);
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, byte[]? value, JsonSerializerOptions options)
    {
        if (value == null || value.Length == 0)
        {
            writer.WriteNullValue();
            return;
        }

        // PostgREST interpreta las cadenas con prefijo "\x" como literales BYTEA en PostgreSQL
        string hex = "\\x" + Convert.ToHexString(value).ToLowerInvariant();
        writer.WriteStringValue(hex);
    }
}
