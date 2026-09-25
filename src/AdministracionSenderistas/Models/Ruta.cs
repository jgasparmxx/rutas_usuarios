using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdministracionSenderistas.Models;

public partial class Ruta : ObservableObject
{
    private string _id = string.Empty;
    [JsonPropertyName("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _nombre = string.Empty;
    [JsonPropertyName("nombre")]
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string? _fecha;
    [JsonPropertyName("fecha")]
    public string? Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    private double? _distanciaGps;
    [JsonPropertyName("distancia_gps")]
    public double? DistanciaGps
    {
        get => _distanciaGps;
        set => SetProperty(ref _distanciaGps, value);
    }

    private string? _emailCreador;
    [JsonPropertyName("email_creador")]
    public string? EmailCreador
    {
        get => _emailCreador;
        set => SetProperty(ref _emailCreador, value);
    }

    private string? _telefonoCreador;
    [JsonPropertyName("telefono_creador")]
    public string? TelefonoCreador
    {
        get => _telefonoCreador;
        set => SetProperty(ref _telefonoCreador, value);
    }

    private bool _publico = true;
    [JsonPropertyName("publico")]
    public bool Publico
    {
        get => _publico;
        set => SetProperty(ref _publico, value);
    }

    private string? _agrupacion;
    [JsonPropertyName("agrupacion")]
    public string? Agrupacion
    {
        get => _agrupacion;
        set => SetProperty(ref _agrupacion, value);
    }

    private string? _dificultad = "Moderada";
    [JsonPropertyName("dificultad")]
    public string? Dificultad
    {
        get => _dificultad;
        set => SetProperty(ref _dificultad, value);
    }

    private string? _duracionEstimada;
    [JsonPropertyName("duracion_estimada")]
    public string? DuracionEstimada
    {
        get => _duracionEstimada;
        set => SetProperty(ref _duracionEstimada, value);
    }

    private string? _descripcion;
    [JsonPropertyName("descripcion")]
    public string? Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    public Ruta Clone() => new()
    {
        Id = Id,
        Nombre = Nombre,
        Fecha = Fecha,
        DistanciaGps = DistanciaGps,
        EmailCreador = EmailCreador,
        TelefonoCreador = TelefonoCreador,
        Publico = Publico,
        Agrupacion = Agrupacion,
        Dificultad = Dificultad,
        DuracionEstimada = DuracionEstimada,
        Descripcion = Descripcion
    };
}
