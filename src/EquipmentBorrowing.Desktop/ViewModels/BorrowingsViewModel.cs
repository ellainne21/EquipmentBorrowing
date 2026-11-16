using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class BorrowingsViewModel : PageViewModelBase
{
    private readonly BorrowingQueryService _query;
    private readonly ReturnEquipmentService _returnService;

    private int _loadVersion;

    public ObservableCollection<ActiveBorrowingDetails> ActiveBorrowings { get; } = new();

    [ObservableProperty]
    public partial ActiveBorrowingDetails? SelectedBorrowing { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public BorrowingsViewModel(BorrowingQueryService query, ReturnEquipmentService returnService)
    {
        _query = query;
        _returnService = returnService;
    }

    // Parameterless constructor is only used by the XAML designer preview
    public BorrowingsViewModel()
    {
        _query = null!;
        _returnService = null!;
    }

    public Task LoadAsync() => ReloadAsync();

    // Also used by the Retry button
    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync();

    [RelayCommand]
    private async Task ReturnAsync()
    {
        // Presentation check only
        if (SelectedBorrowing is null)
        {
            StatusMessage = "Please select a borrowing to return.";
            return;
        }

        var result = await _returnService.ExecuteAsync(SelectedBorrowing.BorrowingId);
        StatusMessage = $"{result.Outcome}: {result.Message}";

        // Always refresh, so a stale or repeated return shows the real current list
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;

        try
        {
            var result = await _query.GetActiveAsync();

            if (version != _loadVersion) return;

            ActiveBorrowings.Clear();

            if (!result.IsSuccess)
            {
                IsEmpty = false;
                LoadError = result.ErrorMessage;
                return;
            }

            foreach (var borrowing in result.Items)
            {
                ActiveBorrowings.Add(borrowing);
            }

            IsEmpty = ActiveBorrowings.Count == 0;
        }
        finally
        {
            if (version == _loadVersion) IsLoading = false;
        }
    }
}
