using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdministracionSenderistas.Models;

public partial class RequisitoSenderismo : ObservableObject
{
    private string _id = string.Empty;
    [JsonPropertyName("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _categoria = "Equipo Básico";
    [JsonPropertyName("categoria")]
    public string Categoria
    {
        get => _categoria;
        set => SetProperty(ref _categoria, value);
    }

    private string _requisito = string.Empty;
    [JsonPropertyName("requisito")]
    public string Requisito
    {
        get => _requisito;
        set => SetProperty(ref _requisito, value);
    }

    private string? _descripcion;
    [JsonPropertyName("descripcion")]
    public string? Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    private bool _esObligatorio = true;
    [JsonPropertyName("es_obligatorio")]
    public bool EsObligatorio
    {
        get => _esObligatorio;
        set => SetProperty(ref _esObligatorio, value);
    }

    private bool _activo = true;
    [JsonPropertyName("activo")]
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    public RequisitoSenderismo Clone() => new()
    {
        Id = Id,
        Categoria = Categoria,
        Requisito = Requisito,
        Descripcion = Descripcion,
        EsObligatorio = EsObligatorio,
        Activo = Activo
    };
}
