using System.Text.Json.Serialization;

namespace AdministracionSenderistas.Models;

public class TrazaPunto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("id_ruta")]
    public string IdRuta { get; set; } = string.Empty;

    [JsonPropertyName("orden")]
    public int Orden { get; set; }

    [JsonPropertyName("latitud")]
    public double Latitud { get; set; }

    [JsonPropertyName("longitud")]
    public double Longitud { get; set; }

    [JsonPropertyName("altitud")]
    public double Altitud { get; set; }
}
