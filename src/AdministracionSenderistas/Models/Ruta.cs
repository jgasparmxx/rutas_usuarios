using System;
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
        set
        {
            if (SetProperty(ref _distanciaGps, value))
            {
                OnPropertyChanged(nameof(DistanciaKmFormateada));
            }
        }
    }

    private double? _distanciaBeacons;
    [JsonPropertyName("distancia_beacons")]
    public double? DistanciaBeacons
    {
        get => _distanciaBeacons;
        set => SetProperty(ref _distanciaBeacons, value);
    }

    private double? _latitudOrig;
    [JsonPropertyName("latitud_orig")]
    public double? LatitudOrig
    {
        get => _latitudOrig;
        set => SetProperty(ref _latitudOrig, value);
    }

    private double? _longitudOrig;
    [JsonPropertyName("longitud_orig")]
    public double? LongitudOrig
    {
        get => _longitudOrig;
        set => SetProperty(ref _longitudOrig, value);
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
    [JsonIgnore]
    public string? Dificultad
    {
        get => _dificultad;
        set => SetProperty(ref _dificultad, value);
    }

    private string? _duracionEstimada;
    [JsonIgnore]
    public string? DuracionEstimada
    {
        get => _duracionEstimada;
        set => SetProperty(ref _duracionEstimada, value);
    }

    private long? _duracionSegundos;
    [JsonPropertyName("duracion_segundos")]
    public long? DuracionSegundos
    {
        get => _duracionSegundos;
        set
        {
            if (SetProperty(ref _duracionSegundos, value))
            {
                OnPropertyChanged(nameof(DuracionFormateada));
            }
        }
    }

    private double? _desnivelPositivo;
    [JsonPropertyName("desnivel_positivo")]
    public double? DesnivelPositivo
    {
        get => _desnivelPositivo;
        set => SetProperty(ref _desnivelPositivo, value);
    }

    private double? _desnivelNegativo;
    [JsonPropertyName("desnivel_negativo")]
    public double? DesnivelNegativo
    {
        get => _desnivelNegativo;
        set => SetProperty(ref _desnivelNegativo, value);
    }

    private int? _contadorSubidas;
    [JsonPropertyName("contador_subidas")]
    public int? ContadorSubidas
    {
        get => _contadorSubidas;
        set => SetProperty(ref _contadorSubidas, value);
    }

    private int? _contadorBajadas;
    [JsonPropertyName("contador_bajadas")]
    public int? ContadorBajadas
    {
        get => _contadorBajadas;
        set => SetProperty(ref _contadorBajadas, value);
    }

    private double? _conectividad;
    [JsonPropertyName("conectividad")]
    public double? Conectividad
    {
        get => _conectividad;
        set => SetProperty(ref _conectividad, value);
    }

    private string? _codigo;
    [JsonPropertyName("codigo")]
    public string? Codigo
    {
        get => _codigo;
        set => SetProperty(ref _codigo, value);
    }

    private string? _fechaHoraInicio;
    [JsonPropertyName("fecha_hora_inicio")]
    public string? FechaHoraInicio
    {
        get => _fechaHoraInicio;
        set => SetProperty(ref _fechaHoraInicio, value);
    }

    private string? _fechaHoraFin;
    [JsonPropertyName("fecha_hora_fin")]
    public string? FechaHoraFin
    {
        get => _fechaHoraFin;
        set => SetProperty(ref _fechaHoraFin, value);
    }

    private string? _fechaRegistroNube;
    [JsonPropertyName("fecha_registro_nube")]
    public string? FechaRegistroNube
    {
        get => _fechaRegistroNube;
        set => SetProperty(ref _fechaRegistroNube, value);
    }

    private string? _datosEncriptados;
    [JsonPropertyName("datos_encriptados")]
    public string? DatosEncriptados
    {
        get => _datosEncriptados;
        set => SetProperty(ref _datosEncriptados, value);
    }

    private string? _descripcion;
    [JsonPropertyName("descripcion")]
    public string? Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    [JsonIgnore]
    public string DistanciaKmFormateada
    {
        get
        {
            if (DistanciaGps == null || DistanciaGps <= 0) return "0.000 km";
            // Si la distancia es > 100 probablemente viene en metros
            double kms = DistanciaGps.Value > 100 ? DistanciaGps.Value / 1000.0 : DistanciaGps.Value;
            return $"{kms:F3} km";
        }
    }

    [JsonIgnore]
    public string DuracionFormateada
    {
        get
        {
            if (DuracionSegundos == null || DuracionSegundos <= 0)
                return !string.IsNullOrWhiteSpace(DuracionEstimada) ? DuracionEstimada : "--:--";

            long totalSec = DuracionSegundos.Value;
            long hrs = totalSec / 3600;
            long mins = (totalSec % 3600) / 60;
            long secs = totalSec % 60;
            return hrs > 0 ? $"{hrs}h {mins}m {secs}s" : $"{mins}m {secs}s";
        }
    }

        private string? _fupdate;
    [JsonPropertyName("fupdate")]
    public string? Fupdate
    {
        get => _fupdate;
        set => SetProperty(ref _fupdate, value);
    }
    public Ruta Clone() => new()
    {
        Id = Id,
        Nombre = Nombre,
        Fecha = Fecha,
        DistanciaGps = DistanciaGps,
        DistanciaBeacons = DistanciaBeacons,
        LatitudOrig = LatitudOrig,
        LongitudOrig = LongitudOrig,
        EmailCreador = EmailCreador,
        TelefonoCreador = TelefonoCreador,
        Publico = Publico,
        Agrupacion = Agrupacion,
        Dificultad = Dificultad,
        DuracionEstimada = DuracionEstimada,
        DuracionSegundos = DuracionSegundos,
        DesnivelPositivo = DesnivelPositivo,
        DesnivelNegativo = DesnivelNegativo,
        ContadorSubidas = ContadorSubidas,
        ContadorBajadas = ContadorBajadas,
        Conectividad = Conectividad,
        Codigo = Codigo,
        FechaHoraInicio = FechaHoraInicio,
        FechaHoraFin = FechaHoraFin,
        FechaRegistroNube = FechaRegistroNube,
        DatosEncriptados = DatosEncriptados,
        Descripcion = Descripcion,
        Fupdate = Fupdate
    };
}

