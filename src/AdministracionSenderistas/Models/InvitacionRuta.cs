using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdministracionSenderistas.Models;

public partial class InvitacionRuta : ObservableObject
{
    private string _idInvitacion = string.Empty;
    [JsonPropertyName("id_invitacion")]
    public string IdInvitacion
    {
        get => _idInvitacion;
        set => SetProperty(ref _idInvitacion, value);
    }

    private string _titulo = string.Empty;
    [JsonPropertyName("titulo")]
    public string Titulo
    {
        get => _titulo;
        set => SetProperty(ref _titulo, value);
    }

    private string? _idRuta;
    [JsonPropertyName("id_ruta")]
    public string? IdRuta
    {
        get => _idRuta;
        set => SetProperty(ref _idRuta, value);
    }

    private string? _fechaSalida;
    [JsonPropertyName("fecha_salida")]
    public string? FechaSalida
    {
        get => _fechaSalida;
        set => SetProperty(ref _fechaSalida, value);
    }

    [JsonPropertyName("fecha_hora_salida")]
    public string? FechaHoraSalida
    {
        get => _fechaSalida;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                SetProperty(ref _fechaSalida, value);
            }
        }
    }

    private string? _telefono;
    [JsonPropertyName("telefono")]
    public string? Telefono
    {
        get => _telefono;
        set => SetProperty(ref _telefono, value);
    }

    private string? _puntoEncuentro;
    [JsonPropertyName("punto_encuentro")]
    public string? PuntoEncuentro
    {
        get => _puntoEncuentro;
        set => SetProperty(ref _puntoEncuentro, value);
    }

    private string? _idAgrupacion;
    [JsonPropertyName("id_agrupacion")]
    public string? IdAgrupacion
    {
        get => _idAgrupacion;
        set => SetProperty(ref _idAgrupacion, value);
    }

    private string? _emailOrganizador;
    [JsonPropertyName("email_organizador")]
    public string? EmailOrganizador
    {
        get => _emailOrganizador;
        set => SetProperty(ref _emailOrganizador, value);
    }

    private int? _cupoMaximo;
    [JsonPropertyName("cupo_maximo")]
    public int? CupoMaximo
    {
        get => _cupoMaximo;
        set => SetProperty(ref _cupoMaximo, value);
    }

    private string? _estado = "Activa";
    [JsonPropertyName("estado")]
    public string? Estado
    {
        get => _estado;
        set => SetProperty(ref _estado, value);
    }

    public InvitacionRuta Clone() => new()
    {
        IdInvitacion = IdInvitacion,
        Titulo = Titulo,
        IdRuta = IdRuta,
        FechaSalida = FechaSalida,
        Telefono = Telefono,
        PuntoEncuentro = PuntoEncuentro,
        IdAgrupacion = IdAgrupacion,
        EmailOrganizador = EmailOrganizador,
        CupoMaximo = CupoMaximo,
        Estado = Estado
    };
}
