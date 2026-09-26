using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AdministracionSenderistas.Models;

namespace AdministracionSenderistas.Services;

public static class CryptoService
{
    private static readonly byte[] SecretKey = new byte[]
    {
        0x5F, 0x2A, 0x1E, 0x7D, 0x4C, 0x3B, 0x0A, 0x7E,
        0x12, 0x5D, 0x4E, 0x3F, 0x2C, 0x1B, 0x0A, 0x6E,
        0x78, 0x69, 0x5A, 0x4B, 0x3C, 0x2D, 0x1E, 0x0F,
        0x7E, 0x6F, 0x5D, 0x4C, 0x3B, 0x2A, 0x19, 0x08
    };

    private const int IvLengthBytes = 12;
    private const int TagLengthBytes = 16;

    public static string DecryptString(string? base64Str)
    {
        if (string.IsNullOrWhiteSpace(base64Str)) return string.Empty;

        try
        {
            byte[] combined = Convert.FromBase64String(base64Str.Trim());
            if (combined.Length < IvLengthBytes + TagLengthBytes)
            {
                return string.Empty;
            }

            byte[] iv = new byte[IvLengthBytes];
            Buffer.BlockCopy(combined, 0, iv, 0, IvLengthBytes);

            int cipherTextLength = combined.Length - IvLengthBytes - TagLengthBytes;
            byte[] cipherText = new byte[cipherTextLength];
            byte[] tag = new byte[TagLengthBytes];

            Buffer.BlockCopy(combined, IvLengthBytes, cipherText, 0, cipherTextLength);
            Buffer.BlockCopy(combined, combined.Length - TagLengthBytes, tag, 0, TagLengthBytes);

            byte[] decrypted = new byte[cipherTextLength];

            using var aesGcm = new AesGcm(SecretKey, TagLengthBytes);
            aesGcm.Decrypt(iv, cipherText, tag, decrypted);

            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CryptoService] Error al desencriptar: {ex.Message}");
            return string.Empty;
        }
    }

    public static DetallesRuta ParseRouteDetails(string? jsonStr)
    {
        var result = new DetallesRuta();
        if (string.IsNullOrWhiteSpace(jsonStr)) return result;

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (root.TryGetProperty("ruta", out var rutaElement) && rutaElement.ValueKind == JsonValueKind.Object)
            {
                result.RutaRaw = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in rutaElement.EnumerateObject())
                {
                    result.RutaRaw[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.TryGetInt64(out long l) ? l : prop.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => prop.Value.GetRawText()
                    };
                }
            }

            if (root.TryGetProperty("puntos", out var puntosElement) && puntosElement.ValueKind == JsonValueKind.Array)
            {
                int pIdx = 1;
                foreach (var p in puntosElement.EnumerateArray())
                {
                    int id = p.TryGetProperty("id", out var idProp) && idProp.TryGetInt32(out int idVal) ? idVal : pIdx;
                    string idRuta = p.TryGetProperty("id_ruta", out var idRutaProp) ? idRutaProp.GetString() ?? "" : "";
                    string fechaHora = p.TryGetProperty("fecha_hora", out var fhProp) ? fhProp.GetString() ?? "" : "";
                    double lat = p.TryGetProperty("latitud", out var latProp) ? latProp.GetDouble() : 0.0;
                    double lon = p.TryGetProperty("longitud", out var lonProp) ? lonProp.GetDouble() : 0.0;
                    double alt = p.TryGetProperty("altitud", out var altProp) ? altProp.GetDouble() : 0.0;
                    string desc = p.TryGetProperty("descripcion", out var descProp) ? descProp.GetString() ?? "" : "";
                    string? foto = p.TryGetProperty("foto_path", out var fotoProp) ? fotoProp.GetString() : null;
                    bool esFin = p.TryGetProperty("es_fin_ruta", out var efProp) && (efProp.ValueKind == JsonValueKind.True || (efProp.ValueKind == JsonValueKind.Number && efProp.GetInt32() == 1));
                    int estado = p.TryGetProperty("estado", out var stProp) && stProp.TryGetInt32(out int stVal) ? stVal : 1;

                    bool conect = false;
                    if (p.TryGetProperty("tiene_conectividad", out var conectProp))
                    {
                        if (conectProp.ValueKind == JsonValueKind.True || conectProp.ValueKind == JsonValueKind.False)
                            conect = conectProp.GetBoolean();
                        else if (conectProp.ValueKind == JsonValueKind.Number)
                            conect = conectProp.GetInt32() == 1;
                        else if (conectProp.ValueKind == JsonValueKind.String)
                            conect = bool.TryParse(conectProp.GetString(), out bool b) && b;
                    }

                    result.Puntos.Add(new PuntoRuta
                    {
                        Id = id,
                        IdRuta = idRuta,
                        FechaHora = fechaHora,
                        Latitud = lat,
                        Longitud = lon,
                        Altitud = alt,
                        Descripcion = desc,
                        FotoPath = string.IsNullOrWhiteSpace(foto) || foto == "null" ? null : foto,
                        EsFinRuta = esFin,
                        TieneConectividad = conect,
                        Estado = estado
                    });
                    pIdx++;
                }
            }

            if (root.TryGetProperty("traza", out var trazaElement) && trazaElement.ValueKind == JsonValueKind.Array)
            {
                int tIdx = 1;
                foreach (var t in trazaElement.EnumerateArray())
                {
                    int id = t.TryGetProperty("id", out var idProp) && idProp.TryGetInt32(out int idVal) ? idVal : tIdx;
                    string idRuta = t.TryGetProperty("id_ruta", out var idRutaProp) ? idRutaProp.GetString() ?? "" : "";
                    double lat = t.TryGetProperty("latitud", out var latProp) ? latProp.GetDouble() : 0.0;
                    double lon = t.TryGetProperty("longitud", out var lonProp) ? lonProp.GetDouble() : 0.0;
                    double alt = t.TryGetProperty("altitud", out var altProp) ? altProp.GetDouble() : 0.0;
                    int orden = t.TryGetProperty("orden", out var ordProp) ? ordProp.GetInt32() : tIdx;

                    result.Traza.Add(new TrazaPunto
                    {
                        Id = id,
                        IdRuta = idRuta,
                        Latitud = lat,
                        Longitud = lon,
                        Altitud = alt,
                        Orden = orden
                    });
                    tIdx++;
                }
                result.Traza.Sort((a, b) => a.Orden.CompareTo(b.Orden));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CryptoService] Error parsing JSON: {ex.Message}, trying regex fallback...");
            FallbackRegexParse(jsonStr, result);
        }

        return result;
    }

    private static void FallbackRegexParse(string jsonStr, DetallesRuta result)
    {
        try
        {
            string cleaned = jsonStr.Replace("\\/", "/");
            int puntosIdx = cleaned.IndexOf("\"puntos\"", StringComparison.OrdinalIgnoreCase);
            int trazaIdx = cleaned.IndexOf("\"traza\"", StringComparison.OrdinalIgnoreCase);

            if (puntosIdx >= 0)
            {
                string puntosBlock = trazaIdx > puntosIdx
                    ? cleaned.Substring(puntosIdx, trazaIdx - puntosIdx)
                    : cleaned.Substring(puntosIdx);

                var objectMatches = Regex.Matches(puntosBlock, @"\{[^{}]*\}");
                foreach (Match m in objectMatches)
                {
                    string txt = m.Value;
                    double lat = ExtractDouble(txt, "latitud");
                    double lon = ExtractDouble(txt, "longitud");
                    string desc = ExtractString(txt, "descripcion");
                    string foto = ExtractString(txt, "foto_path");
                    bool conect = txt.Contains("\"tiene_conectividad\":true") || txt.Contains("\"tiene_conectividad\":1");

                    result.Puntos.Add(new PuntoRuta
                    {
                        Latitud = lat,
                        Longitud = lon,
                        Descripcion = desc,
                        FotoPath = string.IsNullOrWhiteSpace(foto) || foto == "null" ? null : foto,
                        TieneConectividad = conect
                    });
                }
            }

            if (trazaIdx >= 0)
            {
                string trazaBlock = cleaned.Substring(trazaIdx);
                var objectMatches = Regex.Matches(trazaBlock, @"\{[^{}]*\}");
                foreach (Match m in objectMatches)
                {
                    string txt = m.Value;
                    double lat = ExtractDouble(txt, "latitud");
                    double lon = ExtractDouble(txt, "longitud");
                    int orden = (int)ExtractDouble(txt, "orden");

                    result.Traza.Add(new TrazaPunto
                    {
                        Latitud = lat,
                        Longitud = lon,
                        Orden = orden
                    });
                }
                result.Traza.Sort((a, b) => a.Orden.CompareTo(b.Orden));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CryptoService] Fallback regex parse error: {ex.Message}");
        }
    }

    private static double ExtractDouble(string text, string key)
    {
        var match = Regex.Match(text, $"\"{key}\"\\s*:\\s*([-0-9.]+)");
        if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
        {
            return val;
        }
        return 0.0;
    }

    private static string ExtractString(string text, string key)
    {
        var match = Regex.Match(text, $"\"{key}\"\\s*:\\s*\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    public static string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] iv = new byte[IvLengthBytes];
            RandomNumberGenerator.Fill(iv);

            byte[] cipherText = new byte[plainBytes.Length];
            byte[] tag = new byte[TagLengthBytes];

            using var aesGcm = new AesGcm(SecretKey, TagLengthBytes);
            aesGcm.Encrypt(iv, plainBytes, cipherText, tag);

            byte[] combined = new byte[IvLengthBytes + cipherText.Length + TagLengthBytes];
            Buffer.BlockCopy(iv, 0, combined, 0, IvLengthBytes);
            Buffer.BlockCopy(cipherText, 0, combined, IvLengthBytes, cipherText.Length);
            Buffer.BlockCopy(tag, 0, combined, IvLengthBytes + cipherText.Length, TagLengthBytes);

            return Convert.ToBase64String(combined);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CryptoService] Error al encriptar: {ex.Message}");
            return string.Empty;
        }
    }

    public static string SerializeRouteDetails(DetallesRuta details, Ruta? rutaContext = null)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        };

        // 1. Construir objeto "ruta" garantizando el formato exacto requerido por la app de senderismo
        var rutaObj = details.RutaRaw != null
            ? new Dictionary<string, object?>(details.RutaRaw, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (rutaContext != null)
        {
            rutaObj["id"] = rutaContext.Id;
            rutaObj["nombre"] = rutaContext.Nombre;
            rutaObj["fecha"] = rutaContext.Fecha;
            rutaObj["agrupacion"] = rutaContext.Agrupacion;
            rutaObj["latitud_orig"] = rutaContext.LatitudOrig;
            rutaObj["longitud_orig"] = rutaContext.LongitudOrig;
            rutaObj["descripcion"] = rutaContext.Descripcion;
            rutaObj["distancia_gps"] = rutaContext.DistanciaGps;
            rutaObj["distancia_beacons"] = rutaContext.DistanciaBeacons;
            if (!string.IsNullOrEmpty(rutaContext.FechaHoraInicio))
                rutaObj["fecha_hora_inicio"] = rutaContext.FechaHoraInicio;
            if (!string.IsNullOrEmpty(rutaContext.FechaHoraFin))
                rutaObj["fecha_hora_fin"] = rutaContext.FechaHoraFin;
            rutaObj["duracion_segundos"] = rutaContext.DuracionSegundos;
            rutaObj["desnivel_positivo"] = rutaContext.DesnivelPositivo;
            rutaObj["desnivel_negativo"] = rutaContext.DesnivelNegativo;
            rutaObj["contador_subidas"] = rutaContext.ContadorSubidas;
            rutaObj["contador_bajadas"] = rutaContext.ContadorBajadas;
            rutaObj["telefono"] = !string.IsNullOrWhiteSpace(rutaContext.TelefonoCreador)
                ? rutaContext.TelefonoCreador
                : (rutaObj.TryGetValue("telefono", out var tel) && tel != null ? tel.ToString() : "");
            rutaObj["conectividad"] = rutaContext.Conectividad;
            rutaObj["codigo"] = rutaContext.Codigo ?? "";
            rutaObj["publico"] = rutaContext.Publico;
        }

        if (!rutaObj.ContainsKey("id")) rutaObj["id"] = "";
        if (!rutaObj.ContainsKey("nombre")) rutaObj["nombre"] = "";
        if (!rutaObj.ContainsKey("fecha")) rutaObj["fecha"] = DateTime.Now.ToString("dd/MM/yyyy");
        if (!rutaObj.ContainsKey("descripcion")) rutaObj["descripcion"] = "";
        if (!rutaObj.ContainsKey("latitud_orig")) rutaObj["latitud_orig"] = details.Puntos.FirstOrDefault()?.Latitud ?? 0.0;
        if (!rutaObj.ContainsKey("longitud_orig")) rutaObj["longitud_orig"] = details.Puntos.FirstOrDefault()?.Longitud ?? 0.0;

        string routeId = (rutaContext?.Id ?? rutaObj["id"]?.ToString() ?? "").Trim();

        // 2. Homologar puntos asegurando que contengan id, id_ruta y foto_path
        for (int i = 0; i < details.Puntos.Count; i++)
        {
            var p = details.Puntos[i];
            if (p.Id == 0) p.Id = i + 1;
            if (string.IsNullOrWhiteSpace(p.IdRuta) && !string.IsNullOrWhiteSpace(routeId))
                p.IdRuta = routeId;
            if (string.IsNullOrWhiteSpace(p.FechaHora) && rutaContext != null)
                p.FechaHora = rutaContext.Fecha ?? string.Empty;
            if (i == details.Puntos.Count - 1 && details.Puntos.Count > 1)
                p.EsFinRuta = true;
            else if (i < details.Puntos.Count - 1)
                p.EsFinRuta = false;
        }

        // 3. Homologar traza
        for (int i = 0; i < details.Traza.Count; i++)
        {
            var t = details.Traza[i];
            if (t.Id == 0) t.Id = i + 1;
            if (string.IsNullOrWhiteSpace(t.IdRuta) && !string.IsNullOrWhiteSpace(routeId))
                t.IdRuta = routeId;
            if (t.Orden == 0) t.Orden = i + 1;
        }

        var payload = new
        {
            ruta = rutaObj,
            puntos = details.Puntos,
            traza = details.Traza
        };

        return JsonSerializer.Serialize(payload, options);
    }
}
