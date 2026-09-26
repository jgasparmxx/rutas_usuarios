using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdministracionSenderistas.Models;

public partial class ParticipanteInvitacion : ObservableObject
{
    private string _idInvitacion = string.Empty;
    [JsonPropertyName("id_invitacion")]
    public string IdInvitacion
    {
        get => _idInvitacion;
        set => SetProperty(ref _idInvitacion, value);
    }

    private string _emailSenderista = string.Empty;
    [JsonPropertyName("email_senderista")]
    public string EmailSenderista
    {
        get => _emailSenderista;
        set => SetProperty(ref _emailSenderista, value);
    }

    private string? _telefonoSenderista;
    [JsonPropertyName("telefono_senderista")]
    public string? TelefonoSenderista
    {
        get => _telefonoSenderista;
        set => SetProperty(ref _telefonoSenderista, value);
    }

    private string? _estado = "Inscrito"; // Inscrito, Confirmado, Cancelado, Asistio
    [JsonPropertyName("estado")]
    public string? Estado
    {
        get => _estado;
        set => SetProperty(ref _estado, value);
    }

    private string? _fechaRegistro;
    [JsonPropertyName("fecha_registro")]
    public string? FechaRegistro
    {
        get => _fechaRegistro;
        set => SetProperty(ref _fechaRegistro, value);
    }

    public ParticipanteInvitacion Clone() => new()
    {
        IdInvitacion = IdInvitacion,
        EmailSenderista = EmailSenderista,
        TelefonoSenderista = TelefonoSenderista,
        Estado = Estado,
        FechaRegistro = FechaRegistro
    };
}
