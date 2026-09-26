using System.Text.Json.Serialization;

namespace AdministracionSenderistas.Models;

public class PuntoRuta
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("id_ruta")]
    public string IdRuta { get; set; } = string.Empty;

    [JsonPropertyName("fecha_hora")]
    public string FechaHora { get; set; } = string.Empty;

    [JsonPropertyName("latitud")]
    public double Latitud { get; set; }

    [JsonPropertyName("longitud")]
    public double Longitud { get; set; }

    [JsonPropertyName("altitud")]
    public double Altitud { get; set; }

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("foto_path")]
    public string? FotoPath { get; set; }

    [JsonPropertyName("es_fin_ruta")]
    public bool EsFinRuta { get; set; }

    [JsonPropertyName("tiene_conectividad")]
    public bool TieneConectividad { get; set; }

    [JsonPropertyName("estado")]
    public int Estado { get; set; } = 1;

    [JsonIgnore]
    public bool TieneFoto => !string.IsNullOrWhiteSpace(FotoPath) && FotoPath != "null";

    [JsonIgnore]
    public string CoordenadasTexto => $"{Latitud:F3}, {Longitud:F3}";
}
