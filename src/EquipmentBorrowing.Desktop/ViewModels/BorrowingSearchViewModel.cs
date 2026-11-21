using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Desktop.Api;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.ViewModels;

// UC-09: search, filter and sort all borrowing records (active and returned). BR-17, BR-19.
public partial class BorrowingSearchViewModel : PageViewModelBase
{
    private readonly BorrowingsApi _api;

    private int _loadVersion;
    private bool _suppressReload;

    public ObservableCollection<BorrowingRow> Rows { get; } = new();

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    // Index of the chosen item in the Status box: 0 = All, 1 = Active, 2 = Returned
    [ObservableProperty]
    public partial int SelectedStatusIndex { get; set; }

    // Index of the chosen item in the Sort box: 0 = Checkout date, 1 = Expected return date
    [ObservableProperty]
    public partial int SelectedSortIndex { get; set; }

    // Index of the chosen item in the Direction box: 0 = Ascending, 1 = Descending
    [ObservableProperty]
    public partial int SelectedDirectionIndex { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string? EmptyMessage { get; set; }

    [ObservableProperty]
    public partial string? ResultCountText { get; set; }

    public BorrowingSearchViewModel(BorrowingsApi api)
    {
        _api = api;
        ResetCriteria();
    }

    // Parameterless constructor is only used by the XAML designer preview
    public BorrowingSearchViewModel()
    {
        _api = null!;
    }

    public Task LoadAsync() => ReloadAsync();

    [RelayCommand]
    private Task SearchAsync() => ReloadAsync();

    // Also used by the Retry button
    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync();

    // BR-17: criteria can be cleared
    [RelayCommand]
    private Task ClearAsync()
    {
        ResetCriteria();
        return ReloadAsync();
    }

    // Changing a filter or sort box reloads the list at once
    partial void OnSelectedStatusIndexChanged(int value) => ReloadIfReady();
    partial void OnSelectedSortIndexChanged(int value) => ReloadIfReady();
    partial void OnSelectedDirectionIndexChanged(int value) => ReloadIfReady();

    private void ReloadIfReady()
    {
        if (_suppressReload || _api is null) return;
        _ = ReloadAsync();
    }

    // Default view: every record, newest checkout first
    private void ResetCriteria()
    {
        _suppressReload = true;
        SearchText = null;
        SelectedStatusIndex = 0;
        SelectedSortIndex = 0;
        SelectedDirectionIndex = 1;
        _suppressReload = false;
    }

    private async Task ReloadAsync()
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;

        try
        {
            // The three boxes become the API-14 query values
            var status = SelectedStatusIndex switch { 1 => "Active", 2 => "Returned", _ => "All" };
            var sort = SelectedSortIndex == 1 ? "expectedReturnDate" : "borrowedDate";
            var direction = SelectedDirectionIndex == 1 ? "desc" : "asc";

            var result = await _api.SearchAsync(SearchText, status, sort, direction);

            // A newer load has started; drop this old result
            if (version != _loadVersion) return;

            Rows.Clear();

            if (!result.IsSuccess)
            {
                IsEmpty = false;
                ResultCountText = null;
                LoadError = result.Message;
                return;
            }

            foreach (var record in result.Value!)
            {
                Rows.Add(new BorrowingRow(record));
            }

            // "No matches" is an empty state, not an error (BR-17, BR-19)
            IsEmpty = Rows.Count == 0;

            var hasCriteria = !string.IsNullOrWhiteSpace(SearchText) || status != "All";

            EmptyMessage = hasCriteria
                ? "No borrowing records match your search and filters."
                : "No borrowing records exist yet.";

            ResultCountText = Rows.Count == 1 ? "1 record found." : $"{Rows.Count} records found.";
        }
        finally
        {
            if (version == _loadVersion) IsLoading = false;
        }
    }
}

// One row of the result list, with the text already prepared for display.
public class BorrowingRow
{
    // From the API (Lab 5)
    public BorrowingRow(BorrowingDto record)
    {
        BorrowingId = record.Id;
        StudentNumber = record.StudentNumber ?? string.Empty;
        StudentName = record.StudentName ?? string.Empty;
        EquipmentIdentifier = record.EquipmentCode ?? string.Empty;
        EquipmentName = record.EquipmentName ?? string.Empty;
        CheckedOut = record.BorrowedDate.ToString("MM/dd/yyyy");
        Due = record.ExpectedReturnDate.ToString("MM/dd/yyyy");
        Returned = record.ReturnedDate.HasValue ? record.ReturnedDate.Value.ToString("MM/dd/yyyy") : "-";
        Status = record.Status;
    }

    // Old constructor, used by the two history pages until they are migrated (removed in the last cleanup step)
    public BorrowingRow(BorrowingRecord record)
    {
        BorrowingId = record.BorrowingId;
        StudentNumber = record.StudentNumber;
        StudentName = record.StudentName;
        EquipmentIdentifier = record.EquipmentIdentifier;
        EquipmentName = record.EquipmentName;
        CheckedOut = record.DateBorrowed.ToString("MM/dd/yyyy");
        Due = record.ExpectedReturnDate.ToString("MM/dd/yyyy");
        Returned = record.ReturnedDate.HasValue ? record.ReturnedDate.Value.ToString("MM/dd/yyyy") : "-";
        Status = record.Status == BorrowingStatus.Active ? "Active" : "Returned";
    }

    public int BorrowingId { get; }
    public string StudentNumber { get; }
    public string StudentName { get; }
    public string EquipmentIdentifier { get; }
    public string EquipmentName { get; }
    public string CheckedOut { get; }
    public string Due { get; }
    public string Returned { get; }
    public string Status { get; }
}