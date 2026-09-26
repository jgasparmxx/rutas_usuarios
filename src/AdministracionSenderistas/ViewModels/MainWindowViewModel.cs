using AdministracionSenderistas.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdministracionSenderistas.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IDataRepository _repository;
    private readonly ICloudinaryService _cloudinaryService;

    [ObservableProperty]
    private ViewModelBase _currentView;

    public MainWindowViewModel(
        IAuthService authService,
        IDataRepository repository,
        ICloudinaryService? cloudinaryService = null)
    {
        _authService = authService;
        _repository = repository;
        _cloudinaryService = cloudinaryService ?? new CloudinaryService();
        _currentView = null!;
        NavigateToLogin();
    }

    public void NavigateToLogin()
    {
        CurrentView = new LoginViewModel(_authService, NavigateToDashboard);
    }

    public void NavigateToDashboard()
    {
        var dashboardVm = new DashboardViewModel(_repository, _authService, NavigateToLogin, _cloudinaryService);
        CurrentView = dashboardVm;
        _ = dashboardVm.InitializeAsync();
    }
}
