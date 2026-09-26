using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using AdministracionSenderistas.Services;
using AdministracionSenderistas.ViewModels;
using AdministracionSenderistas.Views;

namespace AdministracionSenderistas;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // Core Services
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<ISqliteCacheService, SqliteCacheService>();
        services.AddSingleton<IPostgrestApiClient, PostgrestApiClient>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IDataRepository, DataRepository>();
        services.AddSingleton<ICloudinaryService, CloudinaryService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        Services = services.BuildServiceProvider();

        // Initialize SQLite Cache schema
        var cacheService = Services.GetRequiredService<ISqliteCacheService>();
        await cacheService.InitializeAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowVm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
