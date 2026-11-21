using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Desktop.Api;

namespace EquipmentBorrowing.Desktop.ViewModels;

// One row of the active borrowings list: a display copy of a BorrowingDto from the API.
// It keeps the property names the view already binds to.
public class ActiveBorrowingDetails
{
    public ActiveBorrowingDetails(BorrowingDto borrowing)
    {
        BorrowingId = borrowing.Id;
        StudentNumber = borrowing.StudentNumber ?? string.Empty;
        StudentName = borrowing.StudentName ?? string.Empty;
        EquipmentIdentifier = borrowing.EquipmentCode ?? string.Empty;
        EquipmentName = borrowing.EquipmentName ?? string.Empty;
        DateBorrowed = borrowing.BorrowedDate.ToDateTime(TimeOnly.MinValue);
        ExpectedReturnDate = borrowing.ExpectedReturnDate.ToDateTime(TimeOnly.MinValue);
    }

    public int BorrowingId { get; }
    public string StudentNumber { get; }
    public string StudentName { get; }
    public string EquipmentIdentifier { get; }
    public string EquipmentName { get; }
    public DateTime DateBorrowed { get; }
    public DateTime ExpectedReturnDate { get; }
}

public partial class BorrowingsViewModel : PageViewModelBase
{
    private readonly BorrowingsApi _api;

    private int _loadVersion;

    public ObservableCollection<ActiveBorrowingDetails> ActiveBorrowings { get; } = new();

    [ObservableProperty]
    public partial ActiveBorrowingDetails? SelectedBorrowing { get; set; }

    // Search text typed by the staff member (BR-17). The server does the filtering.
    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string? EmptyMessage { get; set; }

    public BorrowingsViewModel(BorrowingsApi api)
    {
        _api = api;
    }

    // Parameterless constructor is only used by the XAML designer preview
    public BorrowingsViewModel()
    {
        _api = null!;
    }

    public Task LoadAsync() => ReloadAsync();

    // Also used by the Retry button
    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync();

    [RelayCommand]
    private Task SearchAsync() => ReloadAsync();

    [RelayCommand]
    private Task ClearSearchAsync()
    {
        SearchText = null;
        return ReloadAsync();
    }

    // UC-05: return the selected borrowing (API-12)
    [RelayCommand]
    private async Task ReturnAsync()
    {
        // Presentation check only
        if (SelectedBorrowing is null)
        {
            StatusMessage = "Please select a borrowing to return.";
            return;
        }

        var itemName = SelectedBorrowing.EquipmentName;
        IsLoading = true;

        var result = await _api.ReturnAsync(SelectedBorrowing.BorrowingId);
        StatusMessage = result.IsSuccess
            ? $"Returned. {itemName} is available again."
            : Describe(result.Outcome, result.Message);

        // Always refresh from the API, so a stale or repeated return shows the real current list
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;

        try
        {
            var result = await _api.ActiveAsync(SearchText);

            // A newer load has started; drop this old result
            if (version != _loadVersion) return;

            ActiveBorrowings.Clear();

            if (!result.IsSuccess)
            {
                IsEmpty = false;
                LoadError = result.Message;
                return;
            }

            foreach (var borrowing in result.Value!)
            {
                ActiveBorrowings.Add(new ActiveBorrowingDetails(borrowing));
            }

            // "No matches" is an empty state, not an error (BR-17)
            IsEmpty = ActiveBorrowings.Count == 0;
            EmptyMessage = string.IsNullOrWhiteSpace(SearchText)
                ? "There are no active borrowings."
                : "No active borrowings match your search.";
        }
        finally
        {
            if (version == _loadVersion) IsLoading = false;
        }
    }
}