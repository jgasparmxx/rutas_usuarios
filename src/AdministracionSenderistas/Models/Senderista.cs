using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdministracionSenderistas.Models;

public partial class Senderista : ObservableObject
{
    private string _email = string.Empty;
    [JsonPropertyName("email")]
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    private string _nombre = string.Empty;
    [JsonPropertyName("nombre")]
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string _telefono = string.Empty;
    [JsonPropertyName("telefono")]
    public string Telefono
    {
        get => _telefono;
        set => SetProperty(ref _telefono, value);
    }

    private string _rol = "S"; // 'O', 'A', 'G', 'S'
    [JsonPropertyName("rol")]
    public string Rol
    {
        get => _rol;
        set
        {
            if (SetProperty(ref _rol, value))
            {
                OnPropertyChanged(nameof(RolDescripcion));
                OnPropertyChanged(nameof(IsSuperAdmin));
            }
        }
    }

    private string? _idAgrupacion;
    [JsonPropertyName("id_agrupacion")]
    public string? IdAgrupacion
    {
        get => _idAgrupacion;
        set => SetProperty(ref _idAgrupacion, value);
    }

    private string? _sexo = "M";
    [JsonPropertyName("sexo")]
    public string? Sexo
    {
        get => _sexo;
        set => SetProperty(ref _sexo, value);
    }

    private string? _fechaNacimiento;
    [JsonPropertyName("fecha_nacimiento")]
    public string? FechaNacimiento
    {
        get => _fechaNacimiento;
        set => SetProperty(ref _fechaNacimiento, value);
    }

    private string? _aQuienesAvisar;
    [JsonPropertyName("a_quienes_avisar")]
    public string? AQuienesAvisar
    {
        get => _aQuienesAvisar;
        set => SetProperty(ref _aQuienesAvisar, value);
    }

    private string? _aQuienAvisarTelefono1;
    [JsonPropertyName("a_quien_avisar_telefono1")]
    public string? AQuienAvisarTelefono1
    {
        get => _aQuienAvisarTelefono1;
        set => SetProperty(ref _aQuienAvisarTelefono1, value);
    }

    private string? _tieneEnfermedadCronica = "N";
    [JsonPropertyName("tiene_enfermedad_cronica")]
    public string? TieneEnfermedadCronica
    {
        get => _tieneEnfermedadCronica;
        set => SetProperty(ref _tieneEnfermedadCronica, value);
    }

    private bool _bloqueado;
    [JsonPropertyName("bloqueado")]
    public bool Bloqueado
    {
        get => _bloqueado;
        set => SetProperty(ref _bloqueado, value);
    }

    private string? _nivelSenderista = "Intermedio";
    [JsonPropertyName("nivel_senderista")]
    public string? NivelSenderista
    {
        get => _nivelSenderista;
        set => SetProperty(ref _nivelSenderista, value);
    }

    private double? _minimoLongitud = 0;
    [JsonPropertyName("minimo_longitud")]
    public double? MinimoLongitud
    {
        get => _minimoLongitud;
        set
        {
            if (SetProperty(ref _minimoLongitud, value))
            {
                OnPropertyChanged(nameof(MinimoLongitudKm));
            }
        }
    }

    [JsonIgnore]
    public string MinimoLongitudKm => $"{MinimoLongitud ?? 0} km";

    [JsonIgnore]
    public string RolDescripcion => Rol switch
    {
        "O" => "Dueño / Superadmin",
        "A" => "Administrador",
        "G" => "Guía",
        "S" => "Senderista",
        _ => Rol
    };

    [JsonIgnore]
    public bool IsSuperAdmin => Rol == "O";

    public Senderista Clone() => new()
    {
        Email = Email,
        Nombre = Nombre,
        Telefono = Telefono,
        Rol = Rol,
        IdAgrupacion = IdAgrupacion,
        Sexo = Sexo,
        FechaNacimiento = FechaNacimiento,
        AQuienesAvisar = AQuienesAvisar,
        AQuienAvisarTelefono1 = AQuienAvisarTelefono1,
        TieneEnfermedadCronica = TieneEnfermedadCronica,
        Bloqueado = Bloqueado,
        NivelSenderista = NivelSenderista,
        MinimoLongitud = MinimoLongitud
    };
}
