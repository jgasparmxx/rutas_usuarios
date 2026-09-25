using AdministracionSenderistas.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdministracionSenderistas.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IDataRepository _repository;

    [ObservableProperty]
    private ViewModelBase _currentView;

    public MainWindowViewModel(IAuthService authService, IDataRepository repository)
    {
        _authService = authService;
        _repository = repository;
        _currentView = null!;
        NavigateToLogin();
    }

    public void NavigateToLogin()
    {
        CurrentView = new LoginViewModel(_authService, NavigateToDashboard);
    }

    public void NavigateToDashboard()
    {
        var dashboardVm = new DashboardViewModel(_repository, _authService, NavigateToLogin);
        CurrentView = dashboardVm;
        _ = dashboardVm.InitializeAsync();
    }
}
