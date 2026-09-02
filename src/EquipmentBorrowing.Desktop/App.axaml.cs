using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Desktop.Views;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Repositories;
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
        // Repositories - Singleton so in-memory data survives across views
        var studentRepository = new InMemoryStudentRepository();
        var equipmentRepository = new InMemoryEquipmentRepository();
        var borrowingRepository = new InMemoryBorrowingRepository();

        // Seed some starting data so the UI has something to show
        studentRepository.Seed(new Student(1, "Alice Santos", true));
        studentRepository.Seed(new Student(2, "Bob Reyes", false));
        equipmentRepository.Seed(new Equipment(101, "Laptop Dell XPS 15"));
        equipmentRepository.Seed(new Equipment(102, "Projector Epson"));
        equipmentRepository.Seed(new Equipment(103, "Arduino Kit"));

        services.AddSingleton<IStudentRepository>(studentRepository);
        services.AddSingleton<IEquipmentRepository>(equipmentRepository);
        services.AddSingleton<IBorrowingRepository>(borrowingRepository);

        // Application services
        services.AddTransient<BorrowEquipmentService>();
        services.AddTransient<ReturnEquipmentService>();

        // ViewModels
        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }
}