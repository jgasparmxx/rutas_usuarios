using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;
using AdministracionSenderistas.Services;
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
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(SelectedRoleOption)));
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
    private Agrupacion _formAgrupacion = new();

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
        Action onLogout)
    {
        _repository = repository;
        _authService = authService;
        _onLogout = onLogout;

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

    public bool CanCreateNew => CurrentTable != ActiveTable.Senderistas;

    partial void OnCurrentTableChanged(ActiveTable value)
    {
        SearchQuery = string.Empty;
        SelectedTabIndex = 0; // Regresar a Grid al cambiar de tabla
        OnPropertyChanged(nameof(CanCreateNew));
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
        }
        UpdateNavigationState();
    }

    partial void OnSelectedRutaChanged(Ruta? value)
    {
        if (value != null)
        {
            FormRuta = value.Clone();
            IsNewRecord = false;
        }
        UpdateNavigationState();
    }

    partial void OnSelectedAgrupacionChanged(Agrupacion? value)
    {
        if (value != null)
        {
            FormAgrupacion = value.Clone();
            IsNewRecord = false;
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

    public void UpdateNavigationState()
    {
        OnPropertyChanged(nameof(CurrentRecordIndex));
        OnPropertyChanged(nameof(CurrentTotalRecords));
        OnPropertyChanged(nameof(RecordNavigationStatus));
        OnPropertyChanged(nameof(CanNavigatePrevious));
        OnPropertyChanged(nameof(CanNavigateNext));
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
                        (a.Ciudad?.ToLowerInvariant().Contains(q) ?? false));
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
        if (CurrentTable == ActiveTable.Senderistas)
        {
            StatusBanner = "Los senderistas no se pueden dar de alta desde este panel. Los usuarios deben registrarse directamente desde la aplicación móvil.";
            return;
        }

        IsNewRecord = true;
        switch (CurrentTable)
        {
            case ActiveTable.Rutas:
                FormRuta = new Ruta { Id = "RUTA-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmm"), Publico = true };
                break;
            case ActiveTable.Agrupaciones:
                FormAgrupacion = new Agrupacion();
                break;
            case ActiveTable.Invitaciones:
                FormInvitacion = new InvitacionRuta { IdInvitacion = "INV-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmm"), Estado = "Activa" };
                break;
            case ActiveTable.Participantes:
                FormParticipante = new ParticipanteInvitacion { Estado = "Inscrito", FechaRegistro = DateTime.UtcNow.ToString("yyyy-MM-dd") };
                break;
            case ActiveTable.FrasesDificultad:
                FormFraseDificultad = new FraseDificultad { Id = "FRASE-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmm"), Dificultad = "Moderada", Activa = true };
                break;
            case ActiveTable.RequisitosSenderismo:
                FormRequisitoSenderismo = new RequisitoSenderismo { Id = "REQ-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmm"), Categoria = "Equipo Básico", EsObligatorio = true, Activo = true };
                break;
        }

        SelectedTabIndex = 1; // Cambiar a pestaña Formulario
    }

    [RelayCommand]
    public void EditSelected()
    {
        bool hasSelection = CurrentTable switch
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

        if (!hasSelection)
        {
            StatusBanner = "Seleccione un registro de la lista para editar.";
            return;
        }

        IsNewRecord = false;
        UpdateNavigationState();
        SelectedTabIndex = 1; // Ir a pestaña Formulario
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

                        if (string.Equals(FormSenderista.Rol, "A", StringComparison.OrdinalIgnoreCase) && targetAgrupacion != null)
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
                    if (string.IsNullOrWhiteSpace(FormRuta.Id) || string.IsNullOrWhiteSpace(FormRuta.Nombre))
                    {
                        StatusBanner = "El ID y Nombre de la ruta son obligatorios.";
                        return;
                    }
                    success = await _repository.SaveRutaAsync(FormRuta);
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
                await LoadCurrentTableDataAsync();
                SelectedTabIndex = 0; // Regresar al Grid
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
        _onLogout?.Invoke();
    }
}

public class RoleOption
{
    public string Key { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public override string ToString() => Display;
}
