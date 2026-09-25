using System;
using System.Threading.Tasks;
using AdministracionSenderistas.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AdministracionSenderistas.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly Action _onLoginSuccess;

    [ObservableProperty]
    private string _identifier = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isLoading;

    public LoginViewModel(IAuthService authService, Action onLoginSuccess)
    {
        _authService = authService;
        _onLoginSuccess = onLoginSuccess;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(Identifier) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Debe ingresar el correo y la contraseña.";
            return;
        }

        IsLoading = true;
        try
        {
            var (success, message, session) = await _authService.LoginAsync(Identifier, Password);
            if (success && session != null)
            {
                StatusMessage = message;
                _onLoginSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error inesperado al iniciar sesión: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
