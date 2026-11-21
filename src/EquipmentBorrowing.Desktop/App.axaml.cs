using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Desktop.Api;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;

namespace EquipmentBorrowing.Desktop;

public partial class App : Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    // Title label only. The API decides which database is used. Set EQUIPMENT_BORROWING_DB=Test
    // when you start the app after starting the API in test mode, so the window is marked.
    private static bool ShowTestLabel =>
        string.Equals(Environment.GetEnvironmentVariable("EQUIPMENT_BORROWING_DB"), "Test", StringComparison.OrdinalIgnoreCase);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();

            if (ShowTestLabel)
            {
                mainWindow.Title = $"{mainWindow.Title}  [TEST DATABASE]";
            }

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // The desktop talks to the API over HTTP only. There is no database code in this project.
        var apiBase = Environment.GetEnvironmentVariable("EQUIPMENT_BORROWING_API_URL") ?? "http://localhost:5080";
        services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(apiBase.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15)
        });
        services.AddSingleton<ApiClient>();
        services.AddSingleton<StudentsApi>();
        services.AddSingleton<EquipmentApi>();
        services.AddSingleton<BorrowingsApi>();

        // ViewModels
        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<BorrowingSearchViewModel>();
        services.AddTransient<StudentHistoryViewModel>();
        services.AddTransient<EquipmentHistoryViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<EquipmentManagementViewModel>();
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }
}