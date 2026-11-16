using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Desktop.Views;
using EquipmentBorrowing.Infrastructure.Data;
using EquipmentBorrowing.Infrastructure.Repositories;
using EquipmentBorrowing.Infrastructure.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace EquipmentBorrowing.Desktop;

public partial class App : Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    // Set EQUIPMENT_BORROWING_DB=Test to run against the disposable test database.
    private static bool UseTestDatabase =>
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

        // Apply migrations (creates/updates the PostgreSQL schema if needed)
        using (var context = Services.GetRequiredService<AppDbContext>())
        {
            DatabaseInitializer.Initialize(context);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();

            // Make it obvious which database is in use
            if (UseTestDatabase)
            {
                mainWindow.Title = $"{mainWindow.Title}  [TEST DATABASE]";
            }

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // The connection string comes from dotnet user-secrets (never from a file in Git)
        var connectionString = DatabaseSettings.GetConnectionString(UseTestDatabase ? "Test" : "Main");

        // CONTEXT LIFETIME: contexts are short-lived and never shared between operations.
        // - The factory creates one new context per borrow/return operation (unit of work).
        // - Read-only screens use repositories that each get their own context (AsNoTracking reads).
        services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddTransient<AppDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        // Repositories (EF Core / PostgreSQL)
        services.AddTransient<IStudentRepository, EfStudentRepository>();
        services.AddTransient<IEquipmentRepository, EfEquipmentRepository>();
        services.AddTransient<IBorrowingRepository, EfBorrowingRepository>();

        // TRANSACTION BOUNDARY: checkout and return run inside one unit of work (one transaction)
        services.AddSingleton<IBorrowingUnitOfWorkFactory, EfBorrowingUnitOfWorkFactory>();
        services.AddSingleton<ICheckoutFaultInjector>(_ => CreateFaultInjector());

        // Application services
        services.AddTransient<CheckoutTransaction>();
        services.AddTransient<BorrowEquipmentService>();
        services.AddTransient<CheckOutMultipleEquipmentService>();
        services.AddTransient<ReturnEquipmentService>();
        services.AddTransient<RegisterStudentService>();
        services.AddTransient<UpdateStudentService>();
        services.AddTransient<ChangeStudentEligibilityService>();
        services.AddTransient<RegisterEquipmentService>();
        services.AddTransient<UpdateEquipmentService>();
        services.AddTransient<StudentQueryService>();
        services.AddTransient<EquipmentQueryService>();
        services.AddTransient<BorrowingQueryService>();

        // ViewModels
        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<EquipmentManagementViewModel>();
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    // Test seams are honoured ONLY with the disposable test database, never with real data.
    private static ICheckoutFaultInjector CreateFaultInjector()
    {
        if (!UseTestDatabase)
        {
            return new NoCheckoutFaults();
        }

        var mode = Environment.GetEnvironmentVariable("EQUIPMENT_BORROWING_FAULT")?.Trim().ToLowerInvariant();

        return mode switch
        {
            "afterfirstwrite" => new FailAfterFirstWriteInjector(),
            "lostcommit" => new LostCommitResponseInjector(),
            _ => new NoCheckoutFaults()
        };
    }
}
