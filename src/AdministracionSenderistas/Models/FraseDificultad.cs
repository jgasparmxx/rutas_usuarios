using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdministracionSenderistas.Models;

public partial class FraseDificultad : ObservableObject
{
    private string _id = string.Empty;
    [JsonPropertyName("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _dificultad = "Moderada";
    [JsonPropertyName("dificultad")]
    public string Dificultad
    {
        get => _dificultad;
        set => SetProperty(ref _dificultad, value);
    }

    private string _frase = string.Empty;
    [JsonPropertyName("frase")]
    public string Frase
    {
        get => _frase;
        set => SetProperty(ref _frase, value);
    }

    private string? _autor;
    [JsonPropertyName("autor")]
    public string? Autor
    {
        get => _autor;
        set => SetProperty(ref _autor, value);
    }

    private bool _activa = true;
    [JsonPropertyName("activa")]
    public bool Activa
    {
        get => _activa;
        set => SetProperty(ref _activa, value);
    }

    public FraseDificultad Clone() => new()
    {
        Id = Id,
        Dificultad = Dificultad,
        Frase = Frase,
        Autor = Autor,
        Activa = Activa
    };
}
