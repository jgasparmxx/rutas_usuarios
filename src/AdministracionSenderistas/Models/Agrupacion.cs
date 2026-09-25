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

    private string? _descripcion;
    [JsonPropertyName("descripcion")]
    public string? Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    private string? _ciudad;
    [JsonPropertyName("ciudad")]
    public string? Ciudad
    {
        get => _ciudad;
        set => SetProperty(ref _ciudad, value);
    }

    private string? _contacto;
    [JsonPropertyName("contacto")]
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
        Descripcion = Descripcion,
        Ciudad = Ciudad,
        Contacto = Contacto,
        Clave = Clave
    };
}
