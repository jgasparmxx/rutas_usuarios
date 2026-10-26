using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;
using AdministracionSenderistas.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AdministracionSenderistas.ViewModels;

public enum ActiveTable
{
    Senderistas,
    Rutas,
    Agrupaciones,
    Invitaciones,
    Participantes,
    FrasesDificultad,
    RequisitosSenderismo
}

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IDataRepository _repository;
    private readonly IAuthService _authService;
    private readonly Action _onLogout;
    private readonly ICloudinaryService _cloudinaryService;

    // Full data lists
    private List<Senderista> _allSenderistas = new();
    private List<Ruta> _allRutas = new();
    private List<Agrupacion> _allAgrupaciones = new();
    private List<InvitacionRuta> _allInvitaciones = new();
    private List<ParticipanteInvitacion> _allParticipantes = new();
    private List<FraseDificultad> _allFrasesDificultad = new();
    private List<RequisitoSenderismo> _allRequisitosSenderismo = new();

    // Observable collections for Grid
    public ObservableCollection<Senderista> FilteredSenderistas { get; } = new();
    public ObservableCollection<Ruta> FilteredRutas { get; } = new();
    public ObservableCollection<Agrupacion> FilteredAgrupaciones { get; } = new();
    public ObservableCollection<InvitacionRuta> FilteredInvitaciones { get; } = new();
    public ObservableCollection<ParticipanteInvitacion> FilteredParticipantes { get; } = new();
    public ObservableCollection<FraseDificultad> FilteredFrasesDificultad { get; } = new();
    public ObservableCollection<RequisitoSenderismo> FilteredRequisitosSenderismo { get; } = new();

    // Selections
    [ObservableProperty]
    private Senderista? _selectedSenderista;

    [ObservableProperty]
    private Ruta? _selectedRuta;

    [ObservableProperty]
    private Agrupacion? _selectedAgrupacion;

    [ObservableProperty]
    private InvitacionRuta? _selectedInvitacion;

    [ObservableProperty]
    private ParticipanteInvitacion? _selectedParticipante;

    [ObservableProperty]
    private FraseDificultad? _selectedFraseDificultad;

    [ObservableProperty]
    private RequisitoSenderismo? _selectedRequisitoSenderismo;

    [ObservableProperty]
    private Senderista _formSenderista = new();

    private string _originalSenderistaRole = "S";
    private string? _originalSenderistaIdAgrupacion;
    private bool _isInternalRoleUpdate;

    partial void OnFormSenderistaChanged(Senderista value)
    {
        if (!_isInternalRoleUpdate && value != null)
        {
            _originalSenderistaRole = value.Rol ?? "S";
            _originalSenderistaIdAgrupacion = value.IdAgrupacion;
        }
        OnPropertyChanged(nameof(SelectedRoleOption));
        OnPropertyChanged(nameof(SelectedFormAgrupacion));
        NotifySenderistaRoleSecurityProperties();
    }

    public bool IsEditingSelf
    {
        get
        {
            if (FormSenderista == null) return false;

            if (!string.IsNullOrWhiteSpace(CurrentUserEmail) &&
                string.Equals(FormSenderista.Email?.Trim(), CurrentUserEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var sessionTel = _authService.CurrentSession?.Telefono;
            if (!string.IsNullOrWhiteSpace(sessionTel) &&
                !string.IsNullOrWhiteSpace(FormSenderista.Telefono) &&
                string.Equals(sessionTel.Trim(), FormSenderista.Telefono.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }

    public bool IsFormSenderistaSuperUser =>
        string.Equals(FormSenderista?.Rol, "O", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(_originalSenderistaRole, "O", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Un usuario que tiene el rol de 'O' no puede cambiarse el rol a sí mismo,
    /// ni ningún usuario con rol 'O' puede cambiar su rol.
    /// </summary>
    public bool CanEditFormSenderistaRole =>
        !(IsEditingSelf && (CurrentRoleCode == "O" || IsFormSenderistaSuperUser)) &&
        !IsFormSenderistaSuperUser;

    /// <summary>
    /// Un usuario que tiene el rol de 'O' no puede bloquearse a sí mismo.
    /// Cualquier otro usuario (incluyendo otro usuario con rol 'O') sí puede ser bloqueado por un administrador.
    /// </summary>
    public bool CanEditFormSenderistaBloqueado => !IsEditingSelf;

    public string? SenderistaRoleRestrictionMessage
    {
        get
        {
            if (IsEditingSelf && (CurrentRoleCode == "O" || IsFormSenderistaSuperUser))
            {
                return "Restricción de seguridad: Un usuario con rol 'O' no puede cambiarse el rol a sí mismo ni bloquearse.";
            }
            if (IsFormSenderistaSuperUser)
            {
                return "Restricción de seguridad: Los usuarios con rol 'O' no pueden cambiar de rol ni ser eliminados.";
            }
            return null;
        }
    }

    public bool HasSenderistaRoleRestriction => !string.IsNullOrEmpty(SenderistaRoleRestrictionMessage);

    private void NotifySenderistaRoleSecurityProperties()
    {
        OnPropertyChanged(nameof(IsEditingSelf));
        OnPropertyChanged(nameof(IsFormSenderistaSuperUser));
        OnPropertyChanged(nameof(CanEditFormSenderistaRole));
        OnPropertyChanged(nameof(CanEditFormSenderistaBloqueado));
        OnPropertyChanged(nameof(SenderistaRoleRestrictionMessage));
        OnPropertyChanged(nameof(HasSenderistaRoleRestriction));
    }

    public ObservableCollection<RoleOption> AvailableRoles { get; } = new()
    {
        new() { Key = "O", Display = "O - Dueño / Super Administrador" },
        new() { Key = "A", Display = "A - Administrador de Agrupación" },
        new() { Key = "G", Display = "G - Guía de Ruta" },
        new() { Key = "S", Display = "S - Senderista (Estándar)" }
    };

    public ObservableCollection<Agrupacion> AvailableAgrupaciones { get; } = new();

    public Agrupacion? SelectedFormAgrupacion
    {
        get => AvailableAgrupaciones.FirstOrDefault(a => string.Equals(a.IdAgrupacion, FormSenderista?.IdAgrupacion, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (FormSenderista != null)
            {
                FormSenderista.IdAgrupacion = value?.IdAgrupacion;
                OnPropertyChanged();
            }
        }
    }

    public RoleOption? SelectedRoleOption
    {
        get => AvailableRoles.FirstOrDefault(r => string.Equals(r.Key, FormSenderista?.Rol, StringComparison.OrdinalIgnoreCase))
               ?? AvailableRoles.FirstOrDefault(r => r.Key == "S");
        set
        {
            if (_isInternalRoleUpdate) return;
            if (value != null && FormSenderista != null)
            {
                var newRoleKey = value.Key;
                var currentRole = FormSenderista.Rol ?? "S";

                if (string.Equals(newRoleKey, currentRole, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Restricción: Un usuario con rol 'O' no puede cambiarse el rol
                if (!CanEditFormSenderistaRole)
                {
                    RevertRoleSelection(_originalSenderistaRole ?? "O");
                    StatusBanner = SenderistaRoleRestrictionMessage ?? "Restricción: Un usuario con rol 'O' no puede cambiarse el rol.";
                    return;
                }

                // 1. Cuando a un senderista le cambio el rol a 'A' de administrador
                if (string.Equals(newRoleKey, "A", StringComparison.OrdinalIgnoreCase))
                {
                    _ = HandleRoleChangeToAAsync(currentRole);
                    return;
                }

                // 2. Si algún administrador se le cambia el rol
                if (string.Equals(currentRole, "A", StringComparison.OrdinalIgnoreCase))
                {
                    _ = HandleRoleChangeFromAAsync(value);
                    return;
                }

                // Cambio estándar de roles
                FormSenderista.Rol = newRoleKey;
                _originalSenderistaRole = newRoleKey;
                OnPropertyChanged();
                NotifySenderistaRoleSecurityProperties();
            }
        }
    }

    private void RevertRoleSelection(string roleToRestore)
    {
        _isInternalRoleUpdate = true;
        try
        {
            if (FormSenderista != null)
            {
                FormSenderista.Rol = roleToRestore;
            }
            OnPropertyChanged(nameof(SelectedRoleOption));
            try
            {
                Avalonia.Threading.Dispatcher.UIThread?.Post(() => OnPropertyChanged(nameof(SelectedRoleOption)));
            }
            catch { }
        }
        finally
        {
            _isInternalRoleUpdate = false;
        }
    }

    private async Task HandleRoleChangeToAAsync(string previousRole)
    {
        if (FormSenderista == null) return;

        string senderistaNombre = !string.IsNullOrWhiteSpace(FormSenderista.Nombre) ? FormSenderista.Nombre : FormSenderista.Email;

        // 1. Validar que esté asociado a una agrupación
        if (string.IsNullOrWhiteSpace(FormSenderista.IdAgrupacion))
        {
            RevertRoleSelection(previousRole);
            StatusBanner = $"Validación requerida: El senderista '{senderistaNombre}' no está asociado a ninguna agrupación. Debe tener una agrupación registrada para ser Administrador ('A').";
            return;
        }

        // Verificar si id_agrupacion pertenece a alguna agrupación existente
        var ag = AvailableAgrupaciones.FirstOrDefault(a => string.Equals(a.IdAgrupacion, FormSenderista.IdAgrupacion, StringComparison.OrdinalIgnoreCase));
        if (ag == null)
        {
            try
            {
                var list = await _repository.GetAgrupacionesAsync();
                ag = list.FirstOrDefault(a => string.Equals(a.IdAgrupacion, FormSenderista.IdAgrupacion, StringComparison.OrdinalIgnoreCase));
            }
            catch { }
        }

        if (ag == null)
        {
            RevertRoleSelection(previousRole);
            StatusBanner = $"Validación fallida: La agrupación con ID '{FormSenderista.IdAgrupacion}' no fue encontrada en el sistema. No se puede asignar el rol de Administrador ('A').";
            return;
        }

        // Si este filtro pasa correctamente, se le pregunta al que está haciendo el cambio:
        // "¿Deseas que el senderista <nombre> sea administrador de la agrupación <nombre>?"
        string agrupacionNombre = !string.IsNullOrWhiteSpace(ag.Nombre) ? ag.Nombre : ag.IdAgrupacion;

        ShowConfirmationDialog(
            title: "Confirmar Administrador de Agrupación",
            subtitle: "Validación de pertenencia a agrupación aprobada",
            message: $"¿Deseas que el senderista '{senderistaNombre}' sea administrador de la agrupación '{agrupacionNombre}'?",
            icon: "👑",
            isDanger: false,
            confirmText: "Sí, Asignar Administrador",
            onConfirm: async () =>
            {
                IsLoading = true;
                try
                {
                    // Si indica que sí: a la agrupación en la columna de claves se agrega el email del senderista
                    string userEmail = (FormSenderista.Email ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(userEmail))
                    {
                        string currentClave = ag.Clave?.Trim() ?? string.Empty;
                        var emails = currentClave
                            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToList();

                        if (!emails.Any(e => string.Equals(e, userEmail, StringComparison.OrdinalIgnoreCase)))
                        {
                            emails.Add(userEmail);
                            ag.Clave = string.Join(", ", emails);
                        }
                    }

                    // Se asigna el rol 'A' y se actualizan ambas tablas
                    _isInternalRoleUpdate = true;
                    try
                    {
                        FormSenderista.Rol = "A";
                        _originalSenderistaRole = "A";
                        _originalSenderistaIdAgrupacion = FormSenderista.IdAgrupacion;
                        OnPropertyChanged(nameof(SelectedRoleOption));
                    }
                    finally
                    {
                        _isInternalRoleUpdate = false;
                    }

                    await _repository.SaveAgrupacionAsync(ag);
                    await _repository.SaveSenderistaAsync(FormSenderista);

                    // Sincronizar listas en memoria
                    if (SelectedSenderista != null && string.Equals(SelectedSenderista.Email, FormSenderista.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedSenderista.Rol = "A";
                        SelectedSenderista.IdAgrupacion = FormSenderista.IdAgrupacion;
                    }
                    var itemSenderista = FilteredSenderistas.FirstOrDefault(s => string.Equals(s.Email, FormSenderista.Email, StringComparison.OrdinalIgnoreCase));
                    if (itemSenderista != null)
                    {
                        itemSenderista.Rol = "A";
                        itemSenderista.IdAgrupacion = FormSenderista.IdAgrupacion;
                    }

                    var agMemory = _allAgrupaciones.FirstOrDefault(a => string.Equals(a.IdAgrupacion, ag.IdAgrupacion, StringComparison.OrdinalIgnoreCase));
                    if (agMemory != null)
                    {
                        agMemory.Clave = ag.Clave;
                    }
                    if (FormAgrupacion != null && string.Equals(FormAgrupacion.IdAgrupacion, ag.IdAgrupacion, StringComparison.OrdinalIgnoreCase))
                    {
                        FormAgrupacion.Clave = ag.Clave;
                    }

                    RefreshAvailableAgrupaciones();

                    // Avisándole al usuario que se realizó la actualización
                    StatusBanner = $"Se realizó la actualización: El senderista '{senderistaNombre}' ahora es administrador de la agrupación '{agrupacionNombre}'. Ambas tablas fueron actualizadas con éxito.";
                }
                catch (Exception ex)
                {
                    RevertRoleSelection(previousRole);
                    StatusBanner = $"Error al actualizar datos: {ex.Message}";
                }
                finally
                {
                    IsLoading = false;
                }
            },
            onCancel: () =>
            {
                RevertRoleSelection(previousRole);
                StatusBanner = "Operación cancelada: No se cambió el rol a Administrador.";
            }
        );
    }

    private async Task HandleRoleChangeFromAAsync(RoleOption newRole)
    {
        if (FormSenderista == null) return;

        string senderistaNombre = !string.IsNullOrWhiteSpace(FormSenderista.Nombre) ? FormSenderista.Nombre : FormSenderista.Email;
        string userEmail = (FormSenderista.Email ?? string.Empty).Trim();

        // Buscar la agrupación a la que pertenecía
        var ag = AvailableAgrupaciones.FirstOrDefault(a => string.Equals(a.IdAgrupacion, FormSenderista.IdAgrupacion, StringComparison.OrdinalIgnoreCase));
        if (ag == null && !string.IsNullOrWhiteSpace(FormSenderista.IdAgrupacion))
        {
            try
            {
                var list = await _repository.GetAgrupacionesAsync();
                ag = list.FirstOrDefault(a => string.Equals(a.IdAgrupacion, FormSenderista.IdAgrupacion, StringComparison.OrdinalIgnoreCase));
            }
            catch { }
        }

        string agrupacionNombre = ag != null
            ? (!string.IsNullOrWhiteSpace(ag.Nombre) ? ag.Nombre : ag.IdAgrupacion)
            : (FormSenderista.IdAgrupacion ?? "la agrupación");

        ShowConfirmationDialog(
            title: "Confirmar Cambio de Rol de Administrador",
            subtitle: "Se retirará al usuario como administrador y se actualizarán las tablas",
            message: $"¿Deseas cambiar el rol del administrador '{senderistaNombre}' a '{newRole.Display}'?\n\nSu correo ({userEmail}) será eliminado de las claves de la agrupación '{agrupacionNombre}' y se actualizarán ambas tablas.",
            icon: "⚠️",
            isDanger: true,
            confirmText: "Sí, Cambiar Rol",
            onConfirm: async () =>
            {
                IsLoading = true;
                try
                {
                    // 1. Se elimina su email de la columna de claves de la agrupación
                    if (ag != null && !string.IsNullOrWhiteSpace(userEmail))
                    {
                        string currentClave = ag.Clave?.Trim() ?? string.Empty;
                        var emails = currentClave
                            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToList();

                        if (emails.RemoveAll(e => string.Equals(e, userEmail, StringComparison.OrdinalIgnoreCase)) > 0)
                        {
                            ag.Clave = string.Join(", ", emails);
                            await _repository.SaveAgrupacionAsync(ag);
                        }
                    }

                    // Por seguridad, si su email estaba registrado en claves de alguna otra agrupación, retirarlo también
                    if (!string.IsNullOrWhiteSpace(userEmail))
                    {
                        foreach (var otraAg in AvailableAgrupaciones)
                        {
                            if (ag != null && string.Equals(otraAg.IdAgrupacion, ag.IdAgrupacion, StringComparison.OrdinalIgnoreCase))
                                continue;

                            string cClave = otraAg.Clave?.Trim() ?? string.Empty;
                            var eList = cClave
                                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .ToList();

                            if (eList.RemoveAll(e => string.Equals(e, userEmail, StringComparison.OrdinalIgnoreCase)) > 0)
                            {
                                otraAg.Clave = string.Join(", ", eList);
                                await _repository.SaveAgrupacionAsync(otraAg);
                            }
                        }
                    }

                    // 2. Se actualiza el rol del senderista
                    _isInternalRoleUpdate = true;
                    try
                    {
                        FormSenderista.Rol = newRole.Key;
                        _originalSenderistaRole = newRole.Key;
                        _originalSenderistaIdAgrupacion = FormSenderista.IdAgrupacion;
                        OnPropertyChanged(nameof(SelectedRoleOption));
                    }
                    finally
                    {
                        _isInternalRoleUpdate = false;
                    }

                    await _repository.SaveSenderistaAsync(FormSenderista);

                    // Sincronizar listas en memoria
                    if (SelectedSenderista != null && string.Equals(SelectedSenderista.Email, FormSenderista.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedSenderista.Rol = newRole.Key;
                    }
                    var itemSenderista = FilteredSenderistas.FirstOrDefault(s => string.Equals(s.Email, FormSenderista.Email, StringComparison.OrdinalIgnoreCase));
                    if (itemSenderista != null)
                    {
                        itemSenderista.Rol = newRole.Key;
                    }

                    var agMemory = _allAgrupaciones.FirstOrDefault(a => string.Equals(a.IdAgrupacion, ag?.IdAgrupacion, StringComparison.OrdinalIgnoreCase));
                    if (agMemory != null && ag != null)
                    {
                        agMemory.Clave = ag.Clave;
                    }
                    if (ag != null && FormAgrupacion != null && string.Equals(FormAgrupacion.IdAgrupacion, ag.IdAgrupacion, StringComparison.OrdinalIgnoreCase))
                    {
                        FormAgrupacion.Clave = ag.Clave;
                    }

                    RefreshAvailableAgrupaciones();

                    // Avisándole al usuario que se realizó la actualización
                    StatusBanner = $"Se realizó la actualización: Se retiró el rol de administrador para '{senderistaNombre}'. Su correo fue eliminado de las claves de la agrupación y ambas tablas fueron actualizadas con éxito.";
                }
                catch (Exception ex)
                {
                    RevertRoleSelection("A");
                    StatusBanner = $"Error al actualizar datos: {ex.Message}";
                }
                finally
                {
                    IsLoading = false;
                }
            },
            onCancel: () =>
            {
                RevertRoleSelection("A");
                StatusBanner = "Operación cancelada: Se mantuvo el rol de Administrador.";
            }
        );
    }

    [ObservableProperty]
    private Ruta _formRuta = new();

    [ObservableProperty]
    private DetallesRuta _currentRouteDetails = new();

    [ObservableProperty]
    private ObservableCollection<PuntoRutaItem> _currentPuntoItems = new();

    [ObservableProperty]
    private PuntoRutaItem? _selectedPuntoItem;

    [ObservableProperty]
    private bool _isPhotoViewerOpen;

    [ObservableProperty]
    private PuntoRutaItem? _currentViewingPhotoPoint;

    [ObservableProperty]
    private int _routeBookTabIndex; // 0: Puntos de Interés, 1: Mapa Interactivo

    public Agrupacion? SelectedFormRutaAgrupacion
    {
        get => AvailableAgrupaciones.FirstOrDefault(a =>
            string.Equals(a.IdAgrupacion, FormRuta?.Agrupacion, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.Nombre, FormRuta?.Agrupacion, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (FormRuta != null)
            {
                var target = value?.IdAgrupacion ?? value?.Nombre;
                if (!string.Equals(FormRuta.Agrupacion, target, StringComparison.Ordinal))
                {
                    FormRuta.Agrupacion = target;
                    OnPropertyChanged();
                }
            }
        }
    }

    [ObservableProperty]
    private Agrupacion _formAgrupacion = new();

    partial void OnFormAgrupacionChanged(Agrupacion value)
    {
        UpdateAgrupacionLogoBitmap();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAgrupacionLogo))]
    private Bitmap? _formAgrupacionLogoBitmap;

    public bool HasAgrupacionLogo => FormAgrupacionLogoBitmap != null;

    public void UpdateAgrupacionLogoBitmap()
    {
        if (FormAgrupacion?.Logo != null && FormAgrupacion.Logo.Length > 0)
        {
            try
            {
                using var ms = new MemoryStream(FormAgrupacion.Logo);
                FormAgrupacionLogoBitmap = new Bitmap(ms);
            }
            catch
            {
                FormAgrupacionLogoBitmap = null;
            }
        }
        else
        {
            FormAgrupacionLogoBitmap = null;
        }
        OnPropertyChanged(nameof(HasAgrupacionLogo));
    }

    [ObservableProperty]
    private InvitacionRuta _formInvitacion = new();

    [ObservableProperty]
    private ParticipanteInvitacion _formParticipante = new();

    [ObservableProperty]
    private FraseDificultad _formFraseDificultad = new();

    [ObservableProperty]
    private RequisitoSenderismo _formRequisitoSenderismo = new();

    [ObservableProperty]
    private bool _isNewRecord;

    [ObservableProperty]
    private int _selectedTabIndex; // 0: Grid, 1: Formulario

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 1 && !HasSelectedRecord)
        {
            SelectedTabIndex = 0;
            StatusBanner = "Debe seleccionar un registro de la lista antes de consultar o editar en el formulario.";
        }
    }

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    public double SidebarWidth => IsSidebarCollapsed ? 68 : 230;

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarWidth));
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }

    [ObservableProperty]
    private ActiveTable _currentTable = ActiveTable.Senderistas;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _filterRole = "Todos"; // Todos, O, A, G, S

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusBanner = "Listo. Datos sincronizados con caché local SQLite.";

    [ObservableProperty]
    private bool _isBannerVisible = true;

    [ObservableProperty]
    private string _connectionState = "En línea (PostgREST + SQLite)";

    [ObservableProperty]
    private bool _isConfirmDialogVisible;

    [ObservableProperty]
    private string _confirmDialogTitle = "Confirmar Eliminación";

    [ObservableProperty]
    private string _confirmDialogSubtitle = "Advertencia: Esta acción es permanente e irreversible.";

    [ObservableProperty]
    private string _confirmDialogMessage = "¿Está seguro de que desea eliminar este registro?";

    [ObservableProperty]
    private string _confirmDialogIcon = "🗑";

    [ObservableProperty]
    private string _confirmDialogConfirmText = "Sí, Eliminar Definitivamente";

    [ObservableProperty]
    private bool _confirmDialogIsDanger = true;

    partial void OnConfirmDialogIsDangerChanged(bool value)
    {
        OnPropertyChanged(nameof(ConfirmDialogBorderBrush));
        OnPropertyChanged(nameof(ConfirmDialogHeaderBrush));
        OnPropertyChanged(nameof(ConfirmDialogIconBgBrush));
        OnPropertyChanged(nameof(ConfirmDialogSubtitleBrush));
    }

    public string ConfirmDialogBorderBrush => ConfirmDialogIsDanger ? "#EF4444" : "#0284C7";
    public string ConfirmDialogHeaderBrush => ConfirmDialogIsDanger ? "#F87171" : "#38BDF8";
    public string ConfirmDialogIconBgBrush => ConfirmDialogIsDanger ? "#3B1219" : "#0C4A6E";
    public string ConfirmDialogSubtitleBrush => ConfirmDialogIsDanger ? "#FCA5A5" : "#94A3B8";

    private Func<Task>? _pendingDeleteAction;
    private Action? _pendingCancelAction;

    // User info
    public string CurrentUserName => _authService.CurrentSession?.Username ?? "Usuario";
    public string CurrentUserEmail => _authService.CurrentSession?.Email ?? "";
    public string CurrentUserRole => _authService.CurrentSession?.RoleDisplayName ?? "Superadmin";
    public string CurrentRoleCode => _authService.CurrentSession?.Role ?? "O";

    // Table Counts
    [ObservableProperty]
    private int _senderistasCount;

    [ObservableProperty]
    private int _rutasCount;

    [ObservableProperty]
    private int _agrupacionesCount;

    [ObservableProperty]
    private int _invitacionesCount;

    [ObservableProperty]
    private int _participantesCount;

    [ObservableProperty]
    private int _frasesDificultadCount;

    [ObservableProperty]
    private int _requisitosSenderismoCount;

    public DashboardViewModel(
        IDataRepository repository,
        IAuthService authService,
        Action onLogout,
        ICloudinaryService? cloudinaryService = null)
    {
        _repository = repository;
        _authService = authService;
        _onLogout = onLogout;
        _cloudinaryService = cloudinaryService ?? new CloudinaryService();

        _repository.OnSyncStatusChanged += status =>
        {
            StatusBanner = status;
            ConnectionState = _repository.IsOnline ? "En línea (PostgREST)" : "Modo Sin Conexión (SQLite)";
        };
    }

    public async Task InitializeAsync()
    {
        try
        {
            _allAgrupaciones = await _repository.GetAgrupacionesAsync();
            RefreshAvailableAgrupaciones();
        }
        catch { }

        await LoadCurrentTableDataAsync();
    }

    private void RefreshAvailableAgrupaciones()
    {
        AvailableAgrupaciones.Clear();
        foreach (var ag in _allAgrupaciones)
        {
            AvailableAgrupaciones.Add(ag);
        }
        OnPropertyChanged(nameof(SelectedFormAgrupacion));
        ApplyCurrentFilter();
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyCurrentFilter();
    }

    partial void OnFilterRoleChanged(string value)
    {
        ApplyCurrentFilter();
    }

    public bool CanCreateNew => false;

    partial void OnCurrentTableChanged(ActiveTable value)
    {
        SearchQuery = string.Empty;
        SelectedSenderista = null;
        SelectedRuta = null;
        SelectedAgrupacion = null;
        SelectedInvitacion = null;
        SelectedParticipante = null;
        SelectedFraseDificultad = null;
        SelectedRequisitoSenderismo = null;
        SelectedTabIndex = 0; // Regresar a Grid al cambiar de tabla
        OnPropertyChanged(nameof(CanCreateNew));
        OnPropertyChanged(nameof(FormTitle));
        UpdateNavigationState();
        _ = LoadCurrentTableDataAsync();
    }

    partial void OnSelectedSenderistaChanged(Senderista? value)
    {
        if (value != null)
        {
            _isInternalRoleUpdate = true;
            try
            {
                FormSenderista = value.Clone();
                _originalSenderistaRole = FormSenderista.Rol ?? "S";
                _originalSenderistaIdAgrupacion = FormSenderista.IdAgrupacion;
                IsNewRecord = false;
                OnPropertyChanged(nameof(SelectedRoleOption));
                OnPropertyChanged(nameof(SelectedFormAgrupacion));
            }
            finally
            {
                _isInternalRoleUpdate = false;
            }
            NotifySenderistaRoleSecurityProperties();
        }
        UpdateNavigationState();
    }

    partial void OnSelectedRutaChanged(Ruta? value)
    {
        if (value != null)
        {
            FormRuta = value.Clone();
            IsNewRecord = false;
            OnPropertyChanged(nameof(SelectedFormRutaAgrupacion));
            LoadRouteDetails(FormRuta);
        }
        else
        {
            LoadRouteDetails(null);
        }
        UpdateNavigationState();
    }

    partial void OnFormRutaChanged(Ruta value)
    {
        OnPropertyChanged(nameof(SelectedFormRutaAgrupacion));
        OnPropertyChanged(nameof(FormTitle));
        LoadRouteDetails(value);
    }

    private void LoadRouteDetails(Ruta? ruta)
    {
        CurrentPuntoItems.Clear();
        if (ruta == null || string.IsNullOrWhiteSpace(ruta.DatosEncriptados))
        {
            CurrentRouteDetails = new DetallesRuta();
            return;
        }

        try
        {
            string decrypted = CryptoService.DecryptString(ruta.DatosEncriptados);
            var details = CryptoService.ParseRouteDetails(decrypted);
            CurrentRouteDetails = details;

            for (int i = 0; i < details.Puntos.Count; i++)
            {
                CurrentPuntoItems.Add(new PuntoRutaItem(
                    details.Puntos[i],
                    i + 1,
                    OpenPhotoViewer,
                    RequestPointAction,
                    FocusMapOnPunto,
                    RequestDeletePunto));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DashboardViewModel] Error al cargar detalles de la ruta: {ex.Message}");
            CurrentRouteDetails = new DetallesRuta();
        }
    }

    public Action<PuntoRuta>? RequestCenterMapOnPunto { get; set; }

    [RelayCommand]
    public void FocusMapOnPunto(PuntoRutaItem? item)
    {
        if (item == null) return;
        RouteBookTabIndex = 1; // Cambiar automáticamente a la pestaña Recorrido
        RequestCenterMapOnPunto?.Invoke(item.Punto);
    }

    [RelayCommand]
    public void OpenPhotoViewer(PuntoRutaItem? item)
    {
        if (item != null && item.TieneFoto)
        {
            CurrentViewingPhotoPoint = item;
            IsPhotoViewerOpen = true;
        }
    }

    [RelayCommand]
    public void ClosePhotoViewer()
    {
        IsPhotoViewerOpen = false;
        CurrentViewingPhotoPoint = null;
    }

    [ObservableProperty]
    private bool _isPointActionDialogVisible;

    [ObservableProperty]
    private PuntoRutaItem? _targetPointItem;

    [ObservableProperty]
    private bool _isMotiveInputVisible;

    [ObservableProperty]
    private string _removalMotiveText = string.Empty;

    [ObservableProperty]
    private string _pointActionDialogTitle = string.Empty;

    [ObservableProperty]
    private string _pointActionDialogSubtitle = string.Empty;

    [RelayCommand]
    public void RequestPointAction(PuntoRutaItem? item)
    {
        if (item == null) return;
        TargetPointItem = item;
        IsMotiveInputVisible = false;
        RemovalMotiveText = string.Empty;
        PointActionDialogTitle = $"Gestión del Punto #{item.Indice}";
        PointActionDialogSubtitle = $"Coordenadas: {item.CoordenadasFormateadas}";
        IsPointActionDialogVisible = true;
    }

    [RelayCommand]
    public void RequestDeletePunto(PuntoRutaItem? item)
    {
        if (item == null) return;
        TargetPointItem = item;
        IsMotiveInputVisible = false;
        RemovalMotiveText = string.Empty;
        PointActionDialogTitle = $"¿Eliminar Punto #{item.Indice}?";
        PointActionDialogSubtitle = "Se mantendrá la traza GPS para seguir el camino sin alteraciones.";
        IsPointActionDialogVisible = true;
    }

    [RelayCommand]
    public void OpenMotiveInput()
    {
        IsMotiveInputVisible = true;
        if (string.IsNullOrWhiteSpace(RemovalMotiveText))
        {
            RemovalMotiveText = "Fotografía no representativa del camino o sendero";
        }
    }

    [RelayCommand]
    public void BackToPointActionOptions()
    {
        IsMotiveInputVisible = false;
    }

    [RelayCommand]
    public void SelectSuggestedMotive(string? motivo)
    {
        if (!string.IsNullOrWhiteSpace(motivo))
        {
            RemovalMotiveText = motivo;
        }
    }

    [RelayCommand]
    public void ClosePointActionDialog()
    {
        IsPointActionDialogVisible = false;
        IsMotiveInputVisible = false;
        TargetPointItem = null;
        RemovalMotiveText = string.Empty;
    }

    [RelayCommand]
    public async Task ConfirmDeletePointKeepTrazaAsync()
    {
        var item = TargetPointItem;
        if (item == null || CurrentRouteDetails == null || FormRuta == null)
        {
            IsPointActionDialogVisible = false;
            return;
        }

        try
        {
            int oldIndex = item.Indice;
            CurrentRouteDetails.Puntos.Remove(item.Punto);
            CurrentRouteDetails.Puntos = new List<PuntoRuta>(CurrentRouteDetails.Puntos);

            CurrentPuntoItems.Clear();
            for (int i = 0; i < CurrentRouteDetails.Puntos.Count; i++)
            {
                CurrentPuntoItems.Add(new PuntoRutaItem(
                    CurrentRouteDetails.Puntos[i],
                    i + 1,
                    OpenPhotoViewer,
                    RequestPointAction,
                    FocusMapOnPunto,
                    RequestDeletePunto));
            }

            OnPropertyChanged(nameof(CurrentRouteDetails));
            OnPropertyChanged(nameof(CurrentPuntoItems));

            if (IsPhotoViewerOpen && CurrentViewingPhotoPoint == item)
            {
                ClosePhotoViewer();
            }

            IsPointActionDialogVisible = false;
            await SaveRouteDetailsAndStayInFormAsync($"Punto #{oldIndex} eliminado (traza GPS conservada)");
        }
        catch (Exception ex)
        {
            StatusBanner = $"Error al eliminar punto: {ex.Message}";
            IsBannerVisible = true;
        }
    }

    [RelayCommand]
    public async Task ConfirmReplacePhotoWithMotiveAsync()
    {
        var item = TargetPointItem;
        if (item == null || CurrentRouteDetails == null || FormRuta == null)
        {
            IsPointActionDialogVisible = false;
            return;
        }

        try
        {
            string motivo = string.IsNullOrWhiteSpace(RemovalMotiveText)
                ? "Fotografía retirada por moderación"
                : RemovalMotiveText.Trim();

            string puntoInfo = $"Punto #{item.Indice} ({item.CoordenadasTexto})";
            byte[] jpgBytes = ImageGeneratorService.GenerateMotiveJpgBytes(motivo, puntoInfo);

            StatusBanner = "Subiendo imagen de moderación a Cloudinary...";
            IsBannerVisible = true;

            string cleanRouteId = (FormRuta.Id ?? "RUTA").Trim();
            int pointId = item.Punto.Id > 0 ? item.Punto.Id : item.Indice;
            long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string publicId = $"{cleanRouteId}_{pointId}_motivo_{ts}";

            string? cloudUrl = await _cloudinaryService.UploadImageAsync(jpgBytes, publicId, "rutas_senderismo");
            if (string.IsNullOrWhiteSpace(cloudUrl))
            {
                StatusBanner = "Error: No se pudo subir el archivo JPG a Cloudinary. Verifique su conexión a internet.";
                IsBannerVisible = true;
                return;
            }

            item.Punto.FotoPath = cloudUrl;

            using (var ms = new MemoryStream(jpgBytes))
            {
                item.FotoBitmap = new Avalonia.Media.Imaging.Bitmap(ms);
            }
            item.NotifyFotoChanged();

            if (CurrentViewingPhotoPoint == item)
            {
                CurrentViewingPhotoPoint.FotoBitmap = item.FotoBitmap;
            }

            IsPointActionDialogVisible = false;
            IsMotiveInputVisible = false;
            await SaveRouteDetailsAndStayInFormAsync($"Fotografía del Punto #{item.Indice} sustituida por motivo: \"{motivo}\" (Cloudinary JPG)");
        }
        catch (Exception ex)
        {
            StatusBanner = $"Error al sustituir fotografía: {ex.Message}";
            IsBannerVisible = true;
        }
    }

    private async Task SaveRouteDetailsAndStayInFormAsync(string actionSummary)
    {
        if (FormRuta == null || CurrentRouteDetails == null) return;

        try
        {
            IsLoading = true;

            // 1. Serializar y encriptar detalles de la ruta (incluyendo el objeto 'ruta' para compatibilidad total con la app senderista)
            string json = CryptoService.SerializeRouteDetails(CurrentRouteDetails, FormRuta);
            string encrypted = CryptoService.EncryptString(json);
            FormRuta.DatosEncriptados = encrypted;

            // 2. Sincronizar en memoria en _allRutas y SelectedRuta
            var existingRuta = _allRutas.FirstOrDefault(r => string.Equals(r.Id, FormRuta.Id, StringComparison.OrdinalIgnoreCase));
            if (existingRuta != null)
            {
                existingRuta.DatosEncriptados = encrypted;
                existingRuta.Agrupacion = FormRuta.Agrupacion;
                existingRuta.Nombre = FormRuta.Nombre;
            }
            if (SelectedRuta != null && string.Equals(SelectedRuta.Id, FormRuta.Id, StringComparison.OrdinalIgnoreCase))
            {
                SelectedRuta.DatosEncriptados = encrypted;
                SelectedRuta.Agrupacion = FormRuta.Agrupacion;
            }

            // 3. Persistir a PostgreSQL (vía PATCH) y a SQLite (vía Upsert)
            bool apiSaved = await _repository.SaveRutaAsync(existingRuta ?? FormRuta);

            // 4. Asegurar que permanecemos en la Ficha / Formulario de la Ruta
            SelectedTabIndex = 1;

            StatusBanner = apiSaved
                ? $"{actionSummary} • Sincronizado en PostgreSQL (Neon Cloud) y SQLite."
                : $"{actionSummary} • Guardado en caché local SQLite (sin conexión API).";
            IsBannerVisible = true;
        }
        catch (Exception ex)
        {
            StatusBanner = $"Error al sincronizar ruta: {ex.Message}";
            IsBannerVisible = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SaveCurrentRouteAsync()
    {
        await SaveRouteDetailsAndStayInFormAsync("Ruta guardada");
    }

    [RelayCommand]
    public void RequestRemovePhoto(PuntoRutaItem? item)
    {
        RequestPointAction(item);
    }

    public void OnMapPointSelected(PuntoRuta punto)
    {
        var item = CurrentPuntoItems.FirstOrDefault(p => p.Punto == punto ||
            (Math.Abs(p.Latitud - punto.Latitud) < 0.00001 && Math.Abs(p.Longitud - punto.Longitud) < 0.00001));
        if (item != null && item.TieneFoto)
        {
            OpenPhotoViewer(item);
        }
    }

    [ObservableProperty]
    private bool _isRouteDetailsVisible = true;

    partial void OnIsRouteDetailsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(RouteDetailsToggleText));
    }

    public string RouteDetailsToggleText => IsRouteDetailsVisible ? "◀ Ocultar Ficha" : "▶ Ver Ficha de Ruta";

    [RelayCommand]
    public void ToggleRouteDetails()
    {
        IsRouteDetailsVisible = !IsRouteDetailsVisible;
    }

    [RelayCommand]
    public void OpenOpenStreetMapWeb()
    {
        try
        {
            double centerLat = FormRuta?.LatitudOrig ?? 25.6866;
            double centerLon = FormRuta?.LongitudOrig ?? -100.3161;
            if (CurrentRouteDetails?.Traza != null && CurrentRouteDetails.Traza.Count > 0)
            {
                centerLat = CurrentRouteDetails.Traza.Average(t => t.Latitud);
                centerLon = CurrentRouteDetails.Traza.Average(t => t.Longitud);
            }
            string url = FormattableString.Invariant($"https://www.openstreetmap.org/#map=14/{centerLat:F5}/{centerLon:F5}");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusBanner = $"No se pudo abrir OpenStreetMap en el navegador: {ex.Message}";
            IsBannerVisible = true;
        }
    }

    partial void OnSelectedAgrupacionChanged(Agrupacion? value)
    {
        if (value != null)
        {
            FormAgrupacion = value.Clone();
            UpdateAgrupacionLogoBitmap();
            IsNewRecord = false;
        }
        else
        {
            UpdateAgrupacionLogoBitmap();
        }
        UpdateNavigationState();
    }

    partial void OnSelectedInvitacionChanged(InvitacionRuta? value)
    {
        if (value != null)
        {
            FormInvitacion = value.Clone();
            IsNewRecord = false;
        }
        UpdateNavigationState();
    }

    partial void OnSelectedParticipanteChanged(ParticipanteInvitacion? value)
    {
        if (value != null)
        {
            FormParticipante = value.Clone();
            IsNewRecord = false;
        }
        UpdateNavigationState();
    }

    partial void OnSelectedFraseDificultadChanged(FraseDificultad? value)
    {
        if (value != null)
        {
            FormFraseDificultad = value.Clone();
            IsNewRecord = false;
        }
        UpdateNavigationState();
    }

    partial void OnSelectedRequisitoSenderismoChanged(RequisitoSenderismo? value)
    {
        if (value != null)
        {
            FormRequisitoSenderismo = value.Clone();
            IsNewRecord = false;
        }
        UpdateNavigationState();
    }

    // --- BARRA DE NAVEGACIÓN DE REGISTROS EN EL FORMULARIO ---
    public int CurrentRecordIndex
    {
        get
        {
            return CurrentTable switch
            {
                ActiveTable.Senderistas => SelectedSenderista != null ? FilteredSenderistas.IndexOf(SelectedSenderista) : -1,
                ActiveTable.Rutas => SelectedRuta != null ? FilteredRutas.IndexOf(SelectedRuta) : -1,
                ActiveTable.Agrupaciones => SelectedAgrupacion != null ? FilteredAgrupaciones.IndexOf(SelectedAgrupacion) : -1,
                ActiveTable.Invitaciones => SelectedInvitacion != null ? FilteredInvitaciones.IndexOf(SelectedInvitacion) : -1,
                ActiveTable.Participantes => SelectedParticipante != null ? FilteredParticipantes.IndexOf(SelectedParticipante) : -1,
                ActiveTable.FrasesDificultad => SelectedFraseDificultad != null ? FilteredFrasesDificultad.IndexOf(SelectedFraseDificultad) : -1,
                ActiveTable.RequisitosSenderismo => SelectedRequisitoSenderismo != null ? FilteredRequisitosSenderismo.IndexOf(SelectedRequisitoSenderismo) : -1,
                _ => -1
            };
        }
    }

    public int CurrentTotalRecords
    {
        get
        {
            return CurrentTable switch
            {
                ActiveTable.Senderistas => FilteredSenderistas.Count,
                ActiveTable.Rutas => FilteredRutas.Count,
                ActiveTable.Agrupaciones => FilteredAgrupaciones.Count,
                ActiveTable.Invitaciones => FilteredInvitaciones.Count,
                ActiveTable.Participantes => FilteredParticipantes.Count,
                ActiveTable.FrasesDificultad => FilteredFrasesDificultad.Count,
                ActiveTable.RequisitosSenderismo => FilteredRequisitosSenderismo.Count,
                _ => 0
            };
        }
    }

    public bool HasSelectedRecord
    {
        get
        {
            return CurrentTable switch
            {
                ActiveTable.Senderistas => SelectedSenderista != null,
                ActiveTable.Rutas => SelectedRuta != null,
                ActiveTable.Agrupaciones => SelectedAgrupacion != null,
                ActiveTable.Invitaciones => SelectedInvitacion != null,
                ActiveTable.Participantes => SelectedParticipante != null,
                ActiveTable.FrasesDificultad => SelectedFraseDificultad != null,
                ActiveTable.RequisitosSenderismo => SelectedRequisitoSenderismo != null,
                _ => false
            };
        }
    }

    public string FormTitle
    {
        get
        {
            if (CurrentTable == ActiveTable.Rutas)
            {
                var routeName = FormRuta?.Nombre?.Trim();
                if (string.IsNullOrWhiteSpace(routeName))
                {
                    routeName = FormRuta?.Id?.Trim();
                }

                if (!string.IsNullOrWhiteSpace(routeName))
                {
                    return $"Detalle y Edición: {routeName}";
                }
                return IsNewRecord ? "Detalle y Edición: Nueva Ruta" : "Detalle y Edición: Ruta";
            }

            if (CurrentTable == ActiveTable.Senderistas)
            {
                var name = FormSenderista?.Nombre?.Trim();
                if (string.IsNullOrWhiteSpace(name)) name = FormSenderista?.Email?.Trim();
                return !string.IsNullOrWhiteSpace(name) ? $"Detalle y Edición: {name}" : "Detalle y Edición: Senderista";
            }

            if (CurrentTable == ActiveTable.Agrupaciones)
            {
                var name = FormAgrupacion?.Nombre?.Trim();
                return !string.IsNullOrWhiteSpace(name) ? $"Detalle y Edición: {name}" : (IsNewRecord ? "Detalle y Edición: Nueva Agrupación" : "Detalle y Edición: Agrupación");
            }

            if (CurrentTable == ActiveTable.Invitaciones)
            {
                var name = FormInvitacion?.Titulo?.Trim();
                if (string.IsNullOrWhiteSpace(name)) name = FormInvitacion?.IdInvitacion?.Trim();
                return !string.IsNullOrWhiteSpace(name) ? $"Detalle y Edición: {name}" : (IsNewRecord ? "Detalle y Edición: Nueva Invitación" : "Detalle y Edición: Invitación");
            }

            if (CurrentTable == ActiveTable.Participantes)
            {
                var name = FormParticipante?.EmailSenderista?.Trim();
                return !string.IsNullOrWhiteSpace(name) ? $"Detalle y Edición: {name}" : (IsNewRecord ? "Detalle y Edición: Nuevo Participante" : "Detalle y Edición: Participante");
            }

            if (CurrentTable == ActiveTable.FrasesDificultad)
            {
                return IsNewRecord ? "Detalle y Edición: Nueva Frase" : "Detalle y Edición: Frases Dificultad";
            }

            if (CurrentTable == ActiveTable.RequisitosSenderismo)
            {
                return IsNewRecord ? "Detalle y Edición: Nuevo Requisito" : "Detalle y Edición: Requisitos Senderismo";
            }

            return "Detalle y Edición";
        }
    }

    public string RecordNavigationStatus
    {
        get
        {
            int total = CurrentTotalRecords;
            if (total == 0) return "0 de 0";
            int index = CurrentRecordIndex;
            if (index < 0) return $"1 de {total}";
            return $"{index + 1} de {total}";
        }
    }

    public bool CanNavigatePrevious => CurrentRecordIndex > 0;
    public bool CanNavigateNext => CurrentRecordIndex >= 0 && CurrentRecordIndex < CurrentTotalRecords - 1;

    [RelayCommand]
    public void NavigateFirstRecord()
    {
        NavigateToRecordIndex(0);
    }

    [RelayCommand]
    public void NavigatePreviousRecord()
    {
        int current = CurrentRecordIndex;
        if (current > 0)
        {
            NavigateToRecordIndex(current - 1);
        }
    }

    [RelayCommand]
    public void NavigateNextRecord()
    {
        int current = CurrentRecordIndex;
        if (current < CurrentTotalRecords - 1)
        {
            NavigateToRecordIndex(current + 1);
        }
    }

    [RelayCommand]
    public void NavigateLastRecord()
    {
        int total = CurrentTotalRecords;
        if (total > 0)
        {
            NavigateToRecordIndex(total - 1);
        }
    }

    private void NavigateToRecordIndex(int index)
    {
        if (index < 0 || index >= CurrentTotalRecords) return;

        switch (CurrentTable)
        {
            case ActiveTable.Senderistas:
                SelectedSenderista = FilteredSenderistas[index];
                break;
            case ActiveTable.Rutas:
                SelectedRuta = FilteredRutas[index];
                break;
            case ActiveTable.Agrupaciones:
                SelectedAgrupacion = FilteredAgrupaciones[index];
                break;
            case ActiveTable.Invitaciones:
                SelectedInvitacion = FilteredInvitaciones[index];
                break;
            case ActiveTable.Participantes:
                SelectedParticipante = FilteredParticipantes[index];
                break;
            case ActiveTable.FrasesDificultad:
                SelectedFraseDificultad = FilteredFrasesDificultad[index];
                break;
            case ActiveTable.RequisitosSenderismo:
                SelectedRequisitoSenderismo = FilteredRequisitosSenderismo[index];
                break;
        }

        UpdateNavigationState();
    }

    public bool CanDeleteSelected
    {
        get
        {
            return CurrentTable switch
            {
                ActiveTable.Senderistas =>
                    SelectedSenderista != null &&
                    !string.Equals(SelectedSenderista.Rol, "O", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(SelectedSenderista.Email, CurrentUserEmail, StringComparison.OrdinalIgnoreCase) &&
                    !(!string.IsNullOrWhiteSpace(_authService.CurrentSession?.Telefono) && string.Equals(SelectedSenderista.Telefono?.Trim(), _authService.CurrentSession?.Telefono?.Trim(), StringComparison.OrdinalIgnoreCase)),
                ActiveTable.Rutas => SelectedRuta != null,
                ActiveTable.Agrupaciones => SelectedAgrupacion != null,
                ActiveTable.Invitaciones => SelectedInvitacion != null,
                ActiveTable.Participantes => SelectedParticipante != null,
                ActiveTable.FrasesDificultad => SelectedFraseDificultad != null,
                ActiveTable.RequisitosSenderismo => SelectedRequisitoSenderismo != null,
                _ => false
            };
        }
    }

    public bool CanDeleteFormRecord
    {
        get
        {
            if (IsNewRecord) return false;

            return CurrentTable switch
            {
                ActiveTable.Senderistas =>
                    !string.IsNullOrWhiteSpace(FormSenderista.Email) &&
                    !string.Equals(FormSenderista.Rol, "O", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(_originalSenderistaRole, "O", StringComparison.OrdinalIgnoreCase) &&
                    !IsEditingSelf &&
                    !string.Equals(FormSenderista.Email, CurrentUserEmail, StringComparison.OrdinalIgnoreCase),
                ActiveTable.Rutas => !string.IsNullOrWhiteSpace(FormRuta.Id),
                ActiveTable.Agrupaciones => !string.IsNullOrWhiteSpace(FormAgrupacion.IdAgrupacion),
                ActiveTable.Invitaciones => !string.IsNullOrWhiteSpace(FormInvitacion.IdInvitacion),
                ActiveTable.Participantes => !string.IsNullOrWhiteSpace(FormParticipante.IdInvitacion) && !string.IsNullOrWhiteSpace(FormParticipante.EmailSenderista),
                ActiveTable.FrasesDificultad => !string.IsNullOrWhiteSpace(FormFraseDificultad.Id),
                ActiveTable.RequisitosSenderismo => !string.IsNullOrWhiteSpace(FormRequisitoSenderismo.Id),
                _ => false
            };
        }
    }

    public void UpdateNavigationState()
    {
        OnPropertyChanged(nameof(CurrentRecordIndex));
        OnPropertyChanged(nameof(CurrentTotalRecords));
        OnPropertyChanged(nameof(RecordNavigationStatus));
        OnPropertyChanged(nameof(CanNavigatePrevious));
        OnPropertyChanged(nameof(CanNavigateNext));
        OnPropertyChanged(nameof(CanDeleteSelected));
        OnPropertyChanged(nameof(CanDeleteFormRecord));
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(HasSelectedRecord));
        OnPropertyChanged(nameof(CanCreateNew));
    }

    [RelayCommand]
    public void SelectTable(string tableName)
    {
        if (Enum.TryParse<ActiveTable>(tableName, true, out var table))
        {
            CurrentTable = table;
        }
    }

    [RelayCommand]
    public async Task RefreshCurrentTableAsync()
    {
        await LoadCurrentTableDataAsync(forceRefresh: true);
    }

    [RelayCommand]
    public async Task SyncAllTablesAsync()
    {
        IsLoading = true;
        try
        {
            var result = await _repository.SyncAllTablesAsync();
            StatusBanner = result.Message;
            await LoadCurrentTableDataAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadCurrentTableDataAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        try
        {
            switch (CurrentTable)
            {
                case ActiveTable.Senderistas:
                    _allSenderistas = await _repository.GetSenderistasAsync(forceRefresh);
                    SenderistasCount = _allSenderistas.Count;
                    break;
                case ActiveTable.Rutas:
                    _allRutas = await _repository.GetRutasAsync(forceRefresh);
                    RutasCount = _allRutas.Count;
                    break;
                case ActiveTable.Agrupaciones:
                    _allAgrupaciones = await _repository.GetAgrupacionesAsync(forceRefresh);
                    AgrupacionesCount = _allAgrupaciones.Count;
                    RefreshAvailableAgrupaciones();
                    break;
                case ActiveTable.Invitaciones:
                    _allInvitaciones = await _repository.GetInvitacionesAsync(forceRefresh);
                    InvitacionesCount = _allInvitaciones.Count;
                    break;
                case ActiveTable.Participantes:
                    _allParticipantes = await _repository.GetParticipantesAsync(forceRefresh);
                    ParticipantesCount = _allParticipantes.Count;
                    break;
                case ActiveTable.FrasesDificultad:
                    _allFrasesDificultad = await _repository.GetFrasesDificultadAsync(forceRefresh);
                    FrasesDificultadCount = _allFrasesDificultad.Count;
                    break;
                case ActiveTable.RequisitosSenderismo:
                    _allRequisitosSenderismo = await _repository.GetRequisitosSenderismoAsync(forceRefresh);
                    RequisitosSenderismoCount = _allRequisitosSenderismo.Count;
                    break;
            }

            ApplyCurrentFilter();
        }
        catch (Exception ex)
        {
            StatusBanner = $"Error cargando datos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyCurrentFilter()
    {
        string q = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

        switch (CurrentTable)
        {
            case ActiveTable.Senderistas:
                var queryS = _allSenderistas.AsEnumerable();
                if (!string.IsNullOrEmpty(FilterRole) && FilterRole != "Todos")
                {
                    queryS = queryS.Where(s => string.Equals(s.Rol, FilterRole, StringComparison.OrdinalIgnoreCase));
                }
                if (!string.IsNullOrEmpty(q))
                {
                    queryS = queryS.Where(s =>
                        (s.Email?.ToLowerInvariant().Contains(q) ?? false) ||
                        (s.Nombre?.ToLowerInvariant().Contains(q) ?? false) ||
                        (s.Telefono?.ToLowerInvariant().Contains(q) ?? false) ||
                        (s.IdAgrupacion?.ToLowerInvariant().Contains(q) ?? false));
                }
                FilteredSenderistas.Clear();
                foreach (var item in queryS) FilteredSenderistas.Add(item);
                break;

            case ActiveTable.Rutas:
                var queryR = _allRutas.AsEnumerable();
                if (!string.IsNullOrEmpty(q))
                {
                    queryR = queryR.Where(r =>
                        (r.Nombre?.ToLowerInvariant().Contains(q) ?? false) ||
                        (r.Id?.ToLowerInvariant().Contains(q) ?? false) ||
                        (r.Agrupacion?.ToLowerInvariant().Contains(q) ?? false) ||
                        (r.EmailCreador?.ToLowerInvariant().Contains(q) ?? false));
                }
                FilteredRutas.Clear();
                foreach (var item in queryR) FilteredRutas.Add(item);
                break;

            case ActiveTable.Agrupaciones:
                var queryA = _allAgrupaciones.AsEnumerable();
                if (!string.IsNullOrEmpty(q))
                {
                    queryA = queryA.Where(a =>
                        (a.Nombre?.ToLowerInvariant().Contains(q) ?? false) ||
                        (a.IdAgrupacion?.ToLowerInvariant().Contains(q) ?? false) ||
                        (a.Responsable?.ToLowerInvariant().Contains(q) ?? false) ||
                        (a.Email?.ToLowerInvariant().Contains(q) ?? false) ||
                        (a.Telefono?.ToLowerInvariant().Contains(q) ?? false) ||
                        (a.Ciudad?.ToLowerInvariant().Contains(q) ?? false) ||
                        (a.Contacto?.ToLowerInvariant().Contains(q) ?? false));
                }
                FilteredAgrupaciones.Clear();
                foreach (var item in queryA) FilteredAgrupaciones.Add(item);
                break;

            case ActiveTable.Invitaciones:
                var queryI = _allInvitaciones.AsEnumerable();
                if (!string.IsNullOrEmpty(q))
                {
                    queryI = queryI.Where(i =>
                        (i.Titulo?.ToLowerInvariant().Contains(q) ?? false) ||
                        (i.IdInvitacion?.ToLowerInvariant().Contains(q) ?? false) ||
                        (i.IdAgrupacion?.ToLowerInvariant().Contains(q) ?? false));
                }
                FilteredInvitaciones.Clear();
                foreach (var item in queryI) FilteredInvitaciones.Add(item);
                break;

            case ActiveTable.Participantes:
                var queryP = _allParticipantes.AsEnumerable();
                if (!string.IsNullOrEmpty(q))
                {
                    queryP = queryP.Where(p =>
                        (p.EmailSenderista?.ToLowerInvariant().Contains(q) ?? false) ||
                        (p.IdInvitacion?.ToLowerInvariant().Contains(q) ?? false) ||
                        (p.Estado?.ToLowerInvariant().Contains(q) ?? false));
                }
                FilteredParticipantes.Clear();
                foreach (var item in queryP) FilteredParticipantes.Add(item);
                break;

            case ActiveTable.FrasesDificultad:
                var queryF = _allFrasesDificultad.AsEnumerable();
                if (!string.IsNullOrEmpty(q))
                {
                    queryF = queryF.Where(f =>
                        (f.Frase?.ToLowerInvariant().Contains(q) ?? false) ||
                        (f.Dificultad?.ToLowerInvariant().Contains(q) ?? false) ||
                        (f.Autor?.ToLowerInvariant().Contains(q) ?? false) ||
                        (f.Id?.ToLowerInvariant().Contains(q) ?? false));
                }
                FilteredFrasesDificultad.Clear();
                foreach (var item in queryF) FilteredFrasesDificultad.Add(item);
                break;

            case ActiveTable.RequisitosSenderismo:
                var queryReq = _allRequisitosSenderismo.AsEnumerable();
                if (!string.IsNullOrEmpty(q))
                {
                    queryReq = queryReq.Where(rq =>
                        (rq.Requisito?.ToLowerInvariant().Contains(q) ?? false) ||
                        (rq.Categoria?.ToLowerInvariant().Contains(q) ?? false) ||
                        (rq.Descripcion?.ToLowerInvariant().Contains(q) ?? false) ||
                        (rq.Id?.ToLowerInvariant().Contains(q) ?? false));
                }
                FilteredRequisitosSenderismo.Clear();
                foreach (var item in queryReq) FilteredRequisitosSenderismo.Add(item);
                break;
        }

        UpdateNavigationState();
    }

    [RelayCommand]
    public void NewRecord()
    {
        StatusBanner = "Esta aplicación no registra nuevos elementos; su función es únicamente auditar, revisar y ajustar registros existentes.";
    }

    [RelayCommand]
    public void EditSelected()
    {
        if (!HasSelectedRecord)
        {
            StatusBanner = "Debe seleccionar un registro de la lista antes de consultar o editar en el formulario.";
            return;
        }

        IsNewRecord = false;
        UpdateNavigationState();
        SelectedTabIndex = 1; // Ir a pestaña Formulario
    }

    [RelayCommand]
    public async Task SelectAgrupacionLogoAsync()
    {
        try
        {
            IStorageProvider? storage = null;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                storage = desktop.MainWindow?.StorageProvider;
            }

            if (storage == null)
            {
                StatusBanner = "No se pudo acceder al explorador de archivos del sistema.";
                IsBannerVisible = true;
                return;
            }

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Seleccionar Logo de la Agrupación",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    FilePickerFileTypes.ImageAll,
                    new("Archivos de imagen")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.ico" }
                    }
                }
            });

            if (files != null && files.Count > 0)
            {
                var file = files[0];
                await using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                byte[] bytes = ms.ToArray();

                FormAgrupacion.Logo = bytes;
                UpdateAgrupacionLogoBitmap();
                StatusBanner = $"Logo seleccionado ({bytes.Length / 1024.0:F1} KB): {file.Name}. Pulsa 'Guardar Cambios' para sincronizar.";
                IsBannerVisible = true;
            }
        }
        catch (Exception ex)
        {
            StatusBanner = $"Error al seleccionar logo: {ex.Message}";
            IsBannerVisible = true;
        }
    }

    [RelayCommand]
    public void RemoveAgrupacionLogo()
    {
        FormAgrupacion.Logo = null;
        UpdateAgrupacionLogoBitmap();
        StatusBanner = "Logo quitado. Pulsa 'Guardar Cambios' para aplicar en Neon y SQLite.";
        IsBannerVisible = true;
    }

    [RelayCommand]
    public async Task SaveFormAsync()
    {
        IsLoading = true;
        try
        {
            bool success = false;
            switch (CurrentTable)
            {
                case ActiveTable.Senderistas:
                    if (string.IsNullOrWhiteSpace(FormSenderista.Email))
                    {
                        StatusBanner = "El correo del senderista es obligatorio.";
                        return;
                    }

                    // Validación de seguridad para Rol 'O':
                    // Un usuario con rol de 'O' no puede cambiarse el rol a sí mismo ni bloquearse
                    if (IsEditingSelf && (CurrentRoleCode == "O" || string.Equals(_originalSenderistaRole, "O", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!string.Equals(FormSenderista.Rol, "O", StringComparison.OrdinalIgnoreCase))
                        {
                            FormSenderista.Rol = "O";
                            OnPropertyChanged(nameof(SelectedRoleOption));
                            NotifySenderistaRoleSecurityProperties();
                            StatusBanner = "Acción denegada: Un usuario con rol 'O' no puede cambiarse el rol a sí mismo.";
                            return;
                        }

                        if (FormSenderista.Bloqueado)
                        {
                            FormSenderista.Bloqueado = false;
                            NotifySenderistaRoleSecurityProperties();
                            StatusBanner = "Acción denegada: Un usuario con rol 'O' no puede bloquearse a sí mismo.";
                            return;
                        }
                    }


                    Agrupacion? targetAgrupacion = null;
                    if (string.Equals(FormSenderista.Rol, "A", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(FormSenderista.IdAgrupacion))
                        {
                            StatusBanner = "Para asignar el rol 'A' (Administrador), debe seleccionar a qué agrupación pertenece el senderista.";
                            return;
                        }

                        targetAgrupacion = AvailableAgrupaciones.FirstOrDefault(a => string.Equals(a.IdAgrupacion, FormSenderista.IdAgrupacion, StringComparison.OrdinalIgnoreCase));
                        if (targetAgrupacion == null)
                        {
                            var freshList = await _repository.GetAgrupacionesAsync();
                            targetAgrupacion = freshList.FirstOrDefault(a => string.Equals(a.IdAgrupacion, FormSenderista.IdAgrupacion, StringComparison.OrdinalIgnoreCase));
                        }

                        if (targetAgrupacion == null)
                        {
                            StatusBanner = $"La agrupación '{FormSenderista.IdAgrupacion}' no fue encontrada.";
                            return;
                        }

                        // Agregar el correo del senderista en el campo Clave de la agrupación
                        string userEmail = FormSenderista.Email.Trim();
                        string currentClave = targetAgrupacion.Clave?.Trim() ?? string.Empty;
                        var existingEmails = currentClave
                            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToList();

                        if (!existingEmails.Any(e => string.Equals(e, userEmail, StringComparison.OrdinalIgnoreCase)))
                        {
                            existingEmails.Add(userEmail);
                            targetAgrupacion.Clave = string.Join(", ", existingEmails);
                            await _repository.SaveAgrupacionAsync(targetAgrupacion);
                            RefreshAvailableAgrupaciones();
                        }
                    }
                    else if (string.Equals(_originalSenderistaRole, "A", StringComparison.OrdinalIgnoreCase))
                    {
                        // Si era administrador y ahora se guardó con otro rol, retirar su email de las claves de la agrupación
                        string userEmail = FormSenderista.Email.Trim();
                        var prevAgrupacion = AvailableAgrupaciones.FirstOrDefault(a => string.Equals(a.IdAgrupacion, _originalSenderistaIdAgrupacion ?? FormSenderista.IdAgrupacion, StringComparison.OrdinalIgnoreCase));
                        if (prevAgrupacion != null && !string.IsNullOrWhiteSpace(userEmail))
                        {
                            string currentClave = prevAgrupacion.Clave?.Trim() ?? string.Empty;
                            var existingEmails = currentClave
                                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .ToList();

                            if (existingEmails.RemoveAll(e => string.Equals(e, userEmail, StringComparison.OrdinalIgnoreCase)) > 0)
                            {
                                prevAgrupacion.Clave = string.Join(", ", existingEmails);
                                await _repository.SaveAgrupacionAsync(prevAgrupacion);
                                RefreshAvailableAgrupaciones();
                            }
                        }
                    }

                    success = await _repository.SaveSenderistaAsync(FormSenderista);
                    if (success)
                    {
                        _originalSenderistaRole = FormSenderista.Rol;
                        _originalSenderistaIdAgrupacion = FormSenderista.IdAgrupacion;

                        if (FormSenderista.Bloqueado)
                        {
                            var (delParts, delInvs) = await CleanupBlockedUserFutureActivitiesAsync(FormSenderista);
                            string cleanupDetails = (delParts > 0 || delInvs > 0)
                                ? $" Se eliminaron {delParts} participación(es) y {delInvs} convocatoria(s) futuras asociadas al usuario. El historial anterior se conservó para estadísticas."
                                : " No se encontraron participaciones ni convocatorias futuras asociadas. El historial anterior se conservó para estadísticas.";

                            StatusBanner = $"Senderista bloqueado exitosamente.{cleanupDetails}";
                        }
                        else if (string.Equals(FormSenderista.Rol, "A", StringComparison.OrdinalIgnoreCase) && targetAgrupacion != null)
                        {
                            StatusBanner = $"Senderista guardado con éxito. Ahora es administrador de la agrupación '{targetAgrupacion.Nombre}' y ambas tablas han sido actualizadas.";
                        }
                        else
                        {
                            StatusBanner = "Senderista guardado con éxito en ambas fuentes de datos.";
                        }
                    }
                    break;

                case ActiveTable.Rutas:
                    if (string.IsNullOrWhiteSpace(FormRuta.Id))
                    {
                        StatusBanner = "El ID de la ruta es obligatorio.";
                        return;
                    }

                    // Aseguramos serializar y sincronizar los datos encriptados modificados (puntos, traza, fotos)
                    if (CurrentRouteDetails != null)
                    {
                        string json = CryptoService.SerializeRouteDetails(CurrentRouteDetails, FormRuta);
                        FormRuta.DatosEncriptados = CryptoService.EncryptString(json);
                    }

                    var existingRuta = _allRutas.FirstOrDefault(r => string.Equals(r.Id, FormRuta.Id, StringComparison.OrdinalIgnoreCase));
                    if (existingRuta != null)
                    {
                        existingRuta.Agrupacion = FormRuta.Agrupacion;
                        existingRuta.DatosEncriptados = FormRuta.DatosEncriptados;
                        existingRuta.Nombre = FormRuta.Nombre;
                        success = await _repository.SaveRutaAsync(existingRuta);
                        FormRuta = existingRuta.Clone();
                        if (SelectedRuta != null && string.Equals(SelectedRuta.Id, existingRuta.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            SelectedRuta.Agrupacion = existingRuta.Agrupacion;
                            SelectedRuta.DatosEncriptados = existingRuta.DatosEncriptados;
                        }
                    }
                    else
                    {
                        success = await _repository.SaveRutaAsync(FormRuta);
                    }

                    if (success)
                    {
                        StatusBanner = $"Ruta '{FormRuta.Nombre}' sincronizada con éxito en PostgreSQL y SQLite.";
                    }
                    else
                    {
                        StatusBanner = $"Ruta '{FormRuta.Nombre}' guardada solo en caché local SQLite (la API no confirmó el guardado).";
                    }
                    IsBannerVisible = true;
                    break;

                case ActiveTable.Agrupaciones:
                    if (string.IsNullOrWhiteSpace(FormAgrupacion.IdAgrupacion) || string.IsNullOrWhiteSpace(FormAgrupacion.Nombre))
                    {
                        StatusBanner = "El ID y Nombre de la agrupación son obligatorios.";
                        return;
                    }
                    success = await _repository.SaveAgrupacionAsync(FormAgrupacion);
                    break;

                case ActiveTable.Invitaciones:
                    if (string.IsNullOrWhiteSpace(FormInvitacion.IdInvitacion) || string.IsNullOrWhiteSpace(FormInvitacion.Titulo))
                    {
                        StatusBanner = "El ID y Título de la invitación son obligatorios.";
                        return;
                    }
                    success = await _repository.SaveInvitacionAsync(FormInvitacion);
                    break;

                case ActiveTable.Participantes:
                    if (string.IsNullOrWhiteSpace(FormParticipante.IdInvitacion) || string.IsNullOrWhiteSpace(FormParticipante.EmailSenderista))
                    {
                        StatusBanner = "El ID de invitación y Correo del senderista son obligatorios.";
                        return;
                    }
                    success = await _repository.SaveParticipanteAsync(FormParticipante);
                    break;

                case ActiveTable.FrasesDificultad:
                    if (string.IsNullOrWhiteSpace(FormFraseDificultad.Id) || string.IsNullOrWhiteSpace(FormFraseDificultad.Frase))
                    {
                        StatusBanner = "El ID y la Frase son obligatorios.";
                        return;
                    }
                    success = await _repository.SaveFraseDificultadAsync(FormFraseDificultad);
                    break;

                case ActiveTable.RequisitosSenderismo:
                    if (string.IsNullOrWhiteSpace(FormRequisitoSenderismo.Id) || string.IsNullOrWhiteSpace(FormRequisitoSenderismo.Requisito))
                    {
                        StatusBanner = "El ID y el Requisito son obligatorios.";
                        return;
                    }
                    success = await _repository.SaveRequisitoSenderismoAsync(FormRequisitoSenderismo);
                    break;
            }

            if (success)
            {
                StatusBanner = "Registro guardado exitosamente en API y base de datos local SQLite.";
                if (CurrentTable != ActiveTable.Rutas)
                {
                    await LoadCurrentTableDataAsync();
                    SelectedTabIndex = 0; // Regresar al Grid
                }
                else
                {
                    SelectedTabIndex = 1; // Para Rutas, PERMANECER en la Ficha de Ruta para continuar editando
                }
            }
        }
        catch (Exception ex)
        {
            StatusBanner = $"Error al guardar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Compara dos números de teléfono normalizados por sus dígitos o últimos 10 dígitos.
    /// </summary>
    public static bool PhonesMatch(string? phone1, string? phone2)
    {
        if (string.IsNullOrWhiteSpace(phone1) || string.IsNullOrWhiteSpace(phone2)) return false;
        string digits1 = new string(phone1.Where(char.IsDigit).ToArray());
        string digits2 = new string(phone2.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits1) || string.IsNullOrEmpty(digits2)) return false;
        if (digits1 == digits2) return true;
        if (digits1.Length >= 10 && digits2.Length >= 10)
        {
            return digits1.Substring(digits1.Length - 10) == digits2.Substring(digits2.Length - 10);
        }
        return false;
    }

    /// <summary>
    /// Determina si una fecha/hora corresponde a una salida en el futuro.
    /// Las fechas pasadas retornan false para preservarlas con propósitos estadísticos.
    /// </summary>
    public static bool IsFutureDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return false;

        // Formatos ISO 8601 con offset o Z
        if (DateTimeOffset.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dto))
        {
            if (dateStr.Contains(':') || dateStr.Contains('T'))
            {
                return dto > DateTimeOffset.Now;
            }
            return dto.Date >= DateTime.Today;
        }

        // Formatos locales o estándar DateTime
        if (DateTime.TryParse(dateStr, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var dt) ||
            DateTime.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
        {
            if (dateStr.Contains(':') || dateStr.Contains('T'))
            {
                return dt > DateTime.Now;
            }
            return dt.Date >= DateTime.Today;
        }

        return false;
    }

    /// <summary>
    /// Cuando un usuario es bloqueado:
    /// 1. Se eliminan sus inscripciones a salidas con fecha futura (participantes_invitacion). Las pasadas se conservan por estadísticas.
    /// 2. Se eliminan las invitaciones creadas por dicho usuario con fecha futura (invitaciones_ruta, matching por teléfono o email).
    ///    Antes de borrar la invitación, se eliminan sus participantes por integridad referencial. Las pasadas se conservan por estadísticas.
    /// </summary>
    public async Task<(int DeletedParticipaciones, int DeletedInvitaciones)> CleanupBlockedUserFutureActivitiesAsync(Senderista senderista)
    {
        int deletedParts = 0;
        int deletedInvs = 0;

        try
        {
            var allInvitaciones = await _repository.GetInvitacionesAsync();
            var allParticipantes = await _repository.GetParticipantesAsync();

            string userEmail = senderista.Email?.Trim() ?? string.Empty;
            string userTel = senderista.Telefono?.Trim() ?? string.Empty;

            // 1. Verificar si está inscrito en participantes con fecha futura.
            var futureParticipations = allParticipantes.Where(p =>
            {
                bool isUser = (!string.IsNullOrEmpty(userEmail) && string.Equals(p.EmailSenderista, userEmail, StringComparison.OrdinalIgnoreCase)) ||
                              (!string.IsNullOrEmpty(userTel) && PhonesMatch(p.TelefonoSenderista, userTel));
                if (!isUser) return false;

                var inv = allInvitaciones.FirstOrDefault(i => string.Equals(i.IdInvitacion, p.IdInvitacion, StringComparison.OrdinalIgnoreCase));
                if (inv == null) return false;

                string? dateStr = inv.FechaSalida ?? inv.FechaHoraSalida;
                return IsFutureDate(dateStr);
            }).ToList();

            foreach (var part in futureParticipations)
            {
                try
                {
                    await _repository.DeleteParticipanteAsync(part.IdInvitacion, part.EmailSenderista);
                    deletedParts++;
                }
                catch { }
            }

            // 2. Si creó invitaciones (asociado al teléfono del senderista o email del organizador) con fecha futura.
            var futureInvitationsCreated = allInvitaciones.Where(inv =>
            {
                bool isCreator = (!string.IsNullOrEmpty(userTel) && PhonesMatch(inv.Telefono, userTel)) ||
                                 (!string.IsNullOrEmpty(userEmail) && string.Equals(inv.EmailOrganizador, userEmail, StringComparison.OrdinalIgnoreCase));
                if (!isCreator) return false;

                string? dateStr = inv.FechaSalida ?? inv.FechaHoraSalida;
                return IsFutureDate(dateStr);
            }).ToList();

            foreach (var inv in futureInvitationsCreated)
            {
                try
                {
                    // Eliminar primero participantes inscritos a esta invitación (integridad referencial)
                    var enrolled = allParticipantes.Where(p => string.Equals(p.IdInvitacion, inv.IdInvitacion, StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var ep in enrolled)
                    {
                        try
                        {
                            await _repository.DeleteParticipanteAsync(ep.IdInvitacion, ep.EmailSenderista);
                        }
                        catch { }
                    }

                    await _repository.DeleteInvitacionAsync(inv.IdInvitacion);
                    deletedInvs++;
                }
                catch { }
            }

            // Sincronizar listas en memoria si se eliminó algún registro
            if (deletedParts > 0 || deletedInvs > 0)
            {
                _allInvitaciones = await _repository.GetInvitacionesAsync();
                InvitacionesCount = _allInvitaciones.Count;
                _allParticipantes = await _repository.GetParticipantesAsync();
                ParticipantesCount = _allParticipantes.Count;

                if (CurrentTable == ActiveTable.Invitaciones || CurrentTable == ActiveTable.Participantes)
                {
                    ApplyCurrentFilter();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en limpieza de actividades de senderista bloqueado: {ex.Message}");
        }

        return (deletedParts, deletedInvs);
    }

    [RelayCommand]
    public void CancelDelete()
    {
        IsConfirmDialogVisible = false;
        var cancelAction = _pendingCancelAction;
        _pendingDeleteAction = null;
        _pendingCancelAction = null;
        cancelAction?.Invoke();
    }

    [RelayCommand]
    public async Task ConfirmDeleteAsync()
    {
        IsConfirmDialogVisible = false;
        _pendingCancelAction = null;
        if (_pendingDeleteAction != null)
        {
            var action = _pendingDeleteAction;
            _pendingDeleteAction = null;
            await action.Invoke();
        }
    }

    private void ShowDeleteConfirmation(string title, string message, Func<Task> action)
    {
        ConfirmDialogTitle = title;
        ConfirmDialogSubtitle = "Advertencia: Esta acción es permanente e irreversible.";
        ConfirmDialogMessage = message;
        ConfirmDialogIcon = "🗑";
        ConfirmDialogIsDanger = true;
        ConfirmDialogConfirmText = "Sí, Eliminar Definitivamente";
        _pendingCancelAction = null;
        _pendingDeleteAction = action;
        IsConfirmDialogVisible = true;
    }

    private void ShowConfirmationDialog(string title, string subtitle, string message, string icon, bool isDanger, string confirmText, Func<Task> onConfirm, Action? onCancel = null)
    {
        ConfirmDialogTitle = title;
        ConfirmDialogSubtitle = subtitle;
        ConfirmDialogMessage = message;
        ConfirmDialogIcon = icon;
        ConfirmDialogIsDanger = isDanger;
        ConfirmDialogConfirmText = confirmText;
        _pendingDeleteAction = onConfirm;
        _pendingCancelAction = onCancel;
        IsConfirmDialogVisible = true;
    }

    [RelayCommand]
    public void DeleteSelected()
    {
        switch (CurrentTable)
        {
            case ActiveTable.Senderistas:
                if (SelectedSenderista == null)
                {
                    StatusBanner = "Seleccione un senderista de la lista para eliminar.";
                    return;
                }
                var s = SelectedSenderista;
                if (string.Equals(s.Rol, "O", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Email, CurrentUserEmail, StringComparison.OrdinalIgnoreCase))
                {
                    StatusBanner = "Acción denegada: Un usuario con rol 'O' (Dueño / Super Administrador) no puede ser eliminado del sistema.";
                    return;
                }
                ShowDeleteConfirmation(
                    "Confirmar Eliminación de Senderista",
                    $"¿Está seguro de que desea eliminar permanentemente al senderista '{s.Nombre}' ({s.Email})?\n\nEsta operación eliminará el registro tanto en la nube (PostgREST) como en el caché local de SQLite.",
                    async () =>
                    {
                        IsLoading = true;
                        try
                        {
                            await _repository.DeleteSenderistaAsync(s.Email);
                            await LoadCurrentTableDataAsync();
                            StatusBanner = $"Senderista '{s.Email}' eliminado con éxito.";
                        }
                        catch (Exception ex)
                        {
                            StatusBanner = $"Error al eliminar: {ex.Message}";
                        }
                        finally { IsLoading = false; }
                    });
                break;

            case ActiveTable.Rutas:
                if (SelectedRuta == null)
                {
                    StatusBanner = "Seleccione una ruta de la lista para eliminar.";
                    return;
                }
                var r = SelectedRuta;
                ShowDeleteConfirmation(
                    "Confirmar Eliminación de Ruta",
                    $"¿Está seguro de que desea eliminar la ruta '{r.Nombre}' (ID: {r.Id})?\n\nEsta operación se aplicará en la base de datos en la nube y en el caché local.",
                    async () =>
                    {
                        IsLoading = true;
                        try
                        {
                            await _repository.DeleteRutaAsync(r.Id);
                            await LoadCurrentTableDataAsync();
                            StatusBanner = $"Ruta '{r.Id}' eliminada con éxito.";
                        }
                        catch (Exception ex)
                        {
                            StatusBanner = $"Error al eliminar: {ex.Message}";
                        }
                        finally { IsLoading = false; }
                    });
                break;

            case ActiveTable.Agrupaciones:
                if (SelectedAgrupacion == null)
                {
                    StatusBanner = "Seleccione una agrupación para eliminar.";
                    return;
                }
                var a = SelectedAgrupacion;
                ShowDeleteConfirmation(
                    "Confirmar Eliminación de Agrupación",
                    $"¿Está seguro de que desea eliminar la agrupación '{a.Nombre}' (ID: {a.IdAgrupacion})?",
                    async () =>
                    {
                        IsLoading = true;
                        try
                        {
                            await _repository.DeleteAgrupacionAsync(a.IdAgrupacion);
                            await LoadCurrentTableDataAsync();
                            StatusBanner = $"Agrupación '{a.IdAgrupacion}' eliminada.";
                        }
                        catch (Exception ex)
                        {
                            StatusBanner = $"Error al eliminar: {ex.Message}";
                        }
                        finally { IsLoading = false; }
                    });
                break;

            case ActiveTable.Invitaciones:
                if (SelectedInvitacion == null)
                {
                    StatusBanner = "Seleccione una invitación para eliminar.";
                    return;
                }
                var inv = SelectedInvitacion;
                ShowDeleteConfirmation(
                    "Confirmar Eliminación de Convocatoria",
                    $"¿Está seguro de que desea eliminar la invitación '{inv.Titulo}' (ID: {inv.IdInvitacion})?",
                    async () =>
                    {
                        IsLoading = true;
                        try
                        {
                            await _repository.DeleteInvitacionAsync(inv.IdInvitacion);
                            await LoadCurrentTableDataAsync();
                            StatusBanner = $"Invitación '{inv.IdInvitacion}' eliminada.";
                        }
                        catch (Exception ex)
                        {
                            StatusBanner = $"Error al eliminar: {ex.Message}";
                        }
                        finally { IsLoading = false; }
                    });
                break;

            case ActiveTable.Participantes:
                if (SelectedParticipante == null)
                {
                    StatusBanner = "Seleccione un participante para eliminar.";
                    return;
                }
                var part = SelectedParticipante;
                ShowDeleteConfirmation(
                    "Confirmar Eliminación de Participante",
                    $"¿Está seguro de que desea eliminar al participante '{part.EmailSenderista}' de la convocatoria '{part.IdInvitacion}'?",
                    async () =>
                    {
                        IsLoading = true;
                        try
                        {
                            await _repository.DeleteParticipanteAsync(part.IdInvitacion, part.EmailSenderista);
                            await LoadCurrentTableDataAsync();
                            StatusBanner = $"Participante eliminado.";
                        }
                        catch (Exception ex)
                        {
                            StatusBanner = $"Error al eliminar: {ex.Message}";
                        }
                        finally { IsLoading = false; }
                    });
                break;

            case ActiveTable.FrasesDificultad:
                if (SelectedFraseDificultad == null)
                {
                    StatusBanner = "Seleccione una frase para eliminar.";
                    return;
                }
                var f = SelectedFraseDificultad;
                ShowDeleteConfirmation(
                    "Confirmar Eliminación de Frase",
                    $"¿Está seguro de que desea eliminar la frase '{f.Frase}' (ID: {f.Id})?",
                    async () =>
                    {
                        IsLoading = true;
                        try
                        {
                            await _repository.DeleteFraseDificultadAsync(f.Id);
                            await LoadCurrentTableDataAsync();
                            StatusBanner = $"Frase eliminada.";
                        }
                        catch (Exception ex)
                        {
                            StatusBanner = $"Error al eliminar: {ex.Message}";
                        }
                        finally { IsLoading = false; }
                    });
                break;

            case ActiveTable.RequisitosSenderismo:
                if (SelectedRequisitoSenderismo == null)
                {
                    StatusBanner = "Seleccione un requisito para eliminar.";
                    return;
                }
                var req = SelectedRequisitoSenderismo;
                ShowDeleteConfirmation(
                    "Confirmar Eliminación de Requisito",
                    $"¿Está seguro de que desea eliminar el requisito '{req.Requisito}' (ID: {req.Id})?",
                    async () =>
                    {
                        IsLoading = true;
                        try
                        {
                            await _repository.DeleteRequisitoSenderismoAsync(req.Id);
                            await LoadCurrentTableDataAsync();
                            StatusBanner = $"Requisito eliminado.";
                        }
                        catch (Exception ex)
                        {
                            StatusBanner = $"Error al eliminar: {ex.Message}";
                        }
                        finally { IsLoading = false; }
                    });
                break;
        }
    }

    [RelayCommand]
    public void DeleteCurrentFormRecord()
    {
        switch (CurrentTable)
        {
            case ActiveTable.Senderistas:
                if (string.IsNullOrWhiteSpace(FormSenderista.Email)) return;
                if (string.Equals(FormSenderista.Rol, "O", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_originalSenderistaRole, "O", StringComparison.OrdinalIgnoreCase) ||
                    IsEditingSelf ||
                    string.Equals(FormSenderista.Email, CurrentUserEmail, StringComparison.OrdinalIgnoreCase))
                {
                    StatusBanner = "Acción denegada: Un usuario con rol 'O' (Dueño / Super Administrador) no puede ser eliminado del sistema.";
                    return;
                }
                var email = FormSenderista.Email;
                var nombre = FormSenderista.Nombre;
                ShowDeleteConfirmation(
                    "Confirmar Eliminación de Senderista",
                    $"¿Está seguro de que desea eliminar permanentemente al senderista '{nombre}' ({email})?\n\nEsta operación lo borrará del sistema en la nube (PostgREST) y de la base de datos local SQLite.",
                    async () =>
                    {
                        IsLoading = true;
                        try
                        {
                            await _repository.DeleteSenderistaAsync(email);
                            await LoadCurrentTableDataAsync();
                            SelectedTabIndex = 0;
                            StatusBanner = $"Senderista '{email}' eliminado con éxito.";
                        }
                        catch (Exception ex)
                        {
                            StatusBanner = $"Error al eliminar: {ex.Message}";
                        }
                        finally { IsLoading = false; }
                    });
                break;

            case ActiveTable.Rutas:
                if (string.IsNullOrWhiteSpace(FormRuta.Id)) return;
                var rutaId = FormRuta.Id;
                var rutaNombre = FormRuta.Nombre;
                ShowDeleteConfirmation(
                    "Confirmar Eliminación de Ruta",
                    $"¿Está seguro de que desea eliminar la ruta '{rutaNombre}' (ID: {rutaId})?",
                    async () =>
                    {
                        IsLoading = true;
                        try
                        {
                            await _repository.DeleteRutaAsync(rutaId);
                            await LoadCurrentTableDataAsync();
                            SelectedTabIndex = 0;
                            StatusBanner = $"Ruta '{rutaId}' eliminada con éxito.";
                        }
                        catch (Exception ex)
                        {
                            StatusBanner = $"Error al eliminar: {ex.Message}";
                        }
                        finally { IsLoading = false; }
                    });
                break;

            default:
                DeleteSelected();
                break;
        }
    }

    [RelayCommand]
    public void BackToGrid()
    {
        SelectedTabIndex = 0;
    }

    [RelayCommand]
    public void ExportCsv()
    {
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (string.IsNullOrWhiteSpace(desktop))
                desktop = AppDomain.CurrentDomain.BaseDirectory;

            string fileName = $"export_{CurrentTable.ToString().ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string path = Path.Combine(desktop, fileName);

            var sb = new StringBuilder();
            switch (CurrentTable)
            {
                case ActiveTable.Senderistas:
                    sb.AppendLine("Email,Nombre,Telefono,Sexo,Nivel,FechaNacimiento,MinimoLongitudKm,Rol,Bloqueado");
                    foreach (var s in FilteredSenderistas)
                        sb.AppendLine($"\"{s.Email}\",\"{s.Nombre}\",\"{s.Telefono}\",\"{s.Sexo}\",\"{s.NivelSenderista}\",\"{s.FechaNacimiento}\",\"{s.MinimoLongitud ?? 0}\",\"{s.Rol}\",\"{s.Bloqueado}\"");
                    break;
                case ActiveTable.Rutas:
                    sb.AppendLine("Id,Nombre,Fecha,DistanciaKm,EmailCreador,Publico,Agrupacion");
                    foreach (var r in FilteredRutas)
                        sb.AppendLine($"\"{r.Id}\",\"{r.Nombre}\",\"{r.Fecha}\",\"{r.DistanciaGps}\",\"{r.EmailCreador}\",\"{r.Publico}\",\"{r.Agrupacion}\"");
                    break;
                case ActiveTable.Agrupaciones:
                    sb.AppendLine("IdAgrupacion,Nombre,Descripcion,Ciudad,Contacto,Clave");
                    foreach (var a in FilteredAgrupaciones)
                        sb.AppendLine($"\"{a.IdAgrupacion}\",\"{a.Nombre}\",\"{a.Descripcion}\",\"{a.Ciudad}\",\"{a.Contacto}\",\"{a.Clave}\"");
                    break;
                case ActiveTable.Invitaciones:
                    sb.AppendLine("IdInvitacion,Titulo,IdRuta,FechaSalida,PuntoEncuentro,Agrupacion,Estado");
                    foreach (var i in FilteredInvitaciones)
                        sb.AppendLine($"\"{i.IdInvitacion}\",\"{i.Titulo}\",\"{i.IdRuta}\",\"{i.FechaSalida}\",\"{i.PuntoEncuentro}\",\"{i.IdAgrupacion}\",\"{i.Estado}\"");
                    break;
                case ActiveTable.Participantes:
                    sb.AppendLine("IdInvitacion,EmailSenderista,Estado,FechaRegistro");
                    foreach (var p in FilteredParticipantes)
                        sb.AppendLine($"\"{p.IdInvitacion}\",\"{p.EmailSenderista}\",\"{p.Estado}\",\"{p.FechaRegistro}\"");
                    break;
                case ActiveTable.FrasesDificultad:
                    sb.AppendLine("Id,Dificultad,Frase,Autor,Activa");
                    foreach (var f in FilteredFrasesDificultad)
                        sb.AppendLine($"\"{f.Id}\",\"{f.Dificultad}\",\"{f.Frase}\",\"{f.Autor}\",\"{f.Activa}\"");
                    break;
                case ActiveTable.RequisitosSenderismo:
                    sb.AppendLine("Id,Categoria,Requisito,Descripcion,EsObligatorio,Activo");
                    foreach (var rq in FilteredRequisitosSenderismo)
                        sb.AppendLine($"\"{rq.Id}\",\"{rq.Categoria}\",\"{rq.Requisito}\",\"{rq.Descripcion}\",\"{rq.EsObligatorio}\",\"{rq.Activo}\"");
                    break;
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusBanner = $"Datos exportados exitosamente a: {path}";
        }
        catch (Exception ex)
        {
            StatusBanner = $"Error exportando CSV: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Logout()
    {
        _authService.Logout();
        Environment.Exit(0);
    }
}

public class RoleOption
{
    public string Key { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public override string ToString() => Display;
}
