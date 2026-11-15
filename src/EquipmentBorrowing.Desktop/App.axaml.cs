using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Desktop.Views;
using EquipmentBorrowing.Infrastructure.Data;
using EquipmentBorrowing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace EquipmentBorrowing.Desktop;

public partial class App : Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Apply migrations (creates/updates the PostgreSQL schema if needed)
        using (var context = Services.GetRequiredService<AppDbContext>())
        {
            DatabaseInitializer.Initialize(context);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // PostgreSQL connection string comes from dotnet user-secrets (never from a file in Git)
        var connectionString = DatabaseSettings.GetConnectionString("Main");

        services.AddDbContext<AppDbContext>(
            options => options.UseNpgsql(connectionString),
            ServiceLifetime.Transient);

        // Repositories (EF Core / PostgreSQL)
        services.AddTransient<IStudentRepository, EfStudentRepository>();
        services.AddTransient<IEquipmentRepository, EfEquipmentRepository>();
        services.AddTransient<IBorrowingRepository, EfBorrowingRepository>();

        // Application services
        services.AddTransient<BorrowEquipmentService>();
        services.AddTransient<ReturnEquipmentService>();
        services.AddTransient<RegisterStudentService>();
        services.AddTransient<UpdateStudentService>();
        services.AddTransient<ChangeStudentEligibilityService>();
        services.AddTransient<RegisterEquipmentService>();
        services.AddTransient<UpdateEquipmentService>();
        services.AddTransient<StudentQueryService>();
        services.AddTransient<EquipmentQueryService>();

        // ViewModels
        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<EquipmentManagementViewModel>();
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }
}
