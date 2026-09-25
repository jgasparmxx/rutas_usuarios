using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdministracionSenderistas.Models;

public partial class Agrupacion : ObservableObject
{
    private string _idAgrupacion = string.Empty;
    [JsonPropertyName("id_agrupacion")]
    public string IdAgrupacion
    {
        get => _idAgrupacion;
        set => SetProperty(ref _idAgrupacion, value);
    }

    private string _nombre = string.Empty;
    [JsonPropertyName("nombre")]
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string? _responsable;
    [JsonPropertyName("responsable")]
    public string? Responsable
    {
        get => _responsable;
        set => SetProperty(ref _responsable, value);
    }

    private string? _telefono;
    [JsonPropertyName("telefono")]
    public string? Telefono
    {
        get => _telefono;
        set => SetProperty(ref _telefono, value);
    }

    private string? _email;
    [JsonPropertyName("email")]
    public string? Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    private string? _fechaCreacion;
    [JsonPropertyName("fecha_creacion")]
    public string? FechaCreacion
    {
        get => _fechaCreacion;
        set => SetProperty(ref _fechaCreacion, value);
    }

    private string? _descripcion;
    [JsonIgnore]
    public string? Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    private string? _ciudad;
    [JsonIgnore]
    public string? Ciudad
    {
        get => _ciudad;
        set => SetProperty(ref _ciudad, value);
    }

    private string? _contacto;
    [JsonIgnore]
    public string? Contacto
    {
        get => _contacto;
        set => SetProperty(ref _contacto, value);
    }

    private string? _clave;
    [JsonPropertyName("clave")]
    public string? Clave
    {
        get => _clave;
        set => SetProperty(ref _clave, value);
    }

    public Agrupacion Clone() => new()
    {
        IdAgrupacion = IdAgrupacion,
        Nombre = Nombre,
        Responsable = Responsable,
        Telefono = Telefono,
        Email = Email,
        FechaCreacion = FechaCreacion,
        Descripcion = Descripcion,
        Ciudad = Ciudad,
        Contacto = Contacto,
        Clave = Clave
    };
}
