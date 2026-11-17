using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.ViewModels;

// UC-09: search, filter and sort all borrowing records (active and returned). BR-17, BR-19.
public partial class BorrowingSearchViewModel : PageViewModelBase
{
    private readonly BorrowingQueryService _query;

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

    public BorrowingSearchViewModel(BorrowingQueryService query)
    {
        _query = query;
        ResetCriteria();
    }

    // Parameterless constructor is only used by the XAML designer preview
    public BorrowingSearchViewModel()
    {
        _query = null!;
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
        if (_suppressReload || _query is null) return;
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

    private BorrowingSearchCriteria BuildCriteria()
    {
        var status = SelectedStatusIndex switch
        {
            1 => BorrowingStatusFilter.Active,
            2 => BorrowingStatusFilter.Returned,
            _ => BorrowingStatusFilter.All
        };

        var sortField = SelectedSortIndex == 1
            ? BorrowingSortField.ExpectedReturnDate
            : BorrowingSortField.CheckoutDate;

        return new BorrowingSearchCriteria(
            SearchText: SearchText,
            Status: status,
            SortField: sortField,
            Descending: SelectedDirectionIndex == 1);
    }

    private async Task ReloadAsync()
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;

        try
        {
            var criteria = BuildCriteria();
            var result = await _query.SearchAsync(criteria);

            // A newer load has started; drop this old result
            if (version != _loadVersion) return;

            Rows.Clear();

            if (!result.IsSuccess)
            {
                IsEmpty = false;
                ResultCountText = null;
                LoadError = result.ErrorMessage;
                return;
            }

            foreach (var record in result.Items)
            {
                Rows.Add(new BorrowingRow(record));
            }

            // "No matches" is an empty state, not an error (BR-17, BR-19)
            IsEmpty = Rows.Count == 0;

            var hasCriteria = !string.IsNullOrWhiteSpace(criteria.SearchText)
                              || criteria.Status != BorrowingStatusFilter.All;

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
