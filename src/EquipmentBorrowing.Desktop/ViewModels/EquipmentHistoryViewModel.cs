using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Desktop.Api;

namespace EquipmentBorrowing.Desktop.ViewModels;

// One entry of the equipment drop-down on the history page.
public class EquipmentOption
{
    public EquipmentOption(EquipmentDto equipment)
    {
        Id = equipment.Id;
        Label = $"{equipment.EquipmentCode} - {equipment.Name}";
    }

    public int Id { get; }
    public string Label { get; }
}

// UC-08: select an equipment item and see its active and returned borrowings. BR-14, BR-18, BR-19.
public partial class EquipmentHistoryViewModel : PageViewModelBase
{
    private readonly EquipmentApi _equipmentApi;
    private readonly BorrowingsApi _borrowingsApi;

    private int _loadVersion;
    private bool _suppressLoad;

    public ObservableCollection<EquipmentOption> EquipmentItems { get; } = new();
    public ObservableCollection<BorrowingRow> Rows { get; } = new();

    [ObservableProperty]
    public partial EquipmentOption? SelectedEquipment { get; set; }

    // "Select an item" hint, shown while nothing is selected
    [ObservableProperty]
    public partial bool ShowPrompt { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string? EmptyMessage { get; set; }

    [ObservableProperty]
    public partial string? HeaderText { get; set; }

    public EquipmentHistoryViewModel(EquipmentApi equipmentApi, BorrowingsApi borrowingsApi)
    {
        _equipmentApi = equipmentApi;
        _borrowingsApi = borrowingsApi;
    }

    // Parameterless constructor is only used by the XAML designer preview
    public EquipmentHistoryViewModel()
    {
        _equipmentApi = null!;
        _borrowingsApi = null!;
    }

    // Called every time the page is opened: reloads the equipment list, then the history
    public Task LoadAsync() => ReloadAsync(reloadEquipmentList: true);

    // Also used by the Retry button
    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync(reloadEquipmentList: true);

    // Choosing another item replaces the shown records (UC-08 1a)
    partial void OnSelectedEquipmentChanged(EquipmentOption? value)
    {
        if (_suppressLoad || _borrowingsApi is null) return;
        _ = ReloadAsync(reloadEquipmentList: false);
    }

    private void ClearResults()
    {
        Rows.Clear();
        IsEmpty = false;
        HeaderText = null;
    }

    private async Task ReloadAsync(bool reloadEquipmentList)
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;
        StatusMessage = null;

        try
        {
            if (reloadEquipmentList)
            {
                var list = await _equipmentApi.SearchAsync(null, false);

                // A newer load has started; drop this old result
                if (version != _loadVersion) return;

                if (!list.IsSuccess)
                {
                    ClearResults();
                    ShowPrompt = false;
                    LoadError = list.Message;
                    return;
                }

                var previousId = SelectedEquipment?.Id;

                _suppressLoad = true;
                EquipmentItems.Clear();
                foreach (var equipment in list.Value!)
                {
                    EquipmentItems.Add(new EquipmentOption(equipment));
                }
                SelectedEquipment = previousId.HasValue
                    ? EquipmentItems.FirstOrDefault(e => e.Id == previousId.Value)
                    : null;
                _suppressLoad = false;

                // The selected item is no longer in the list: explain and ask to choose again
                if (previousId.HasValue && SelectedEquipment is null)
                {
                    ClearResults();
                    ShowPrompt = true;
                    StatusMessage = "The previously selected equipment is no longer available. Please select an item.";
                    return;
                }
            }

            if (SelectedEquipment is null)
            {
                ClearResults();
                ShowPrompt = true;
                return;
            }

            ShowPrompt = false;
            var selected = SelectedEquipment;
            var result = await _borrowingsApi.EquipmentHistoryAsync(selected.Id);

            if (version != _loadVersion) return;

            // The server says the item does not exist any more (404)
            if (result.Outcome == ApiOutcome.NotFound)
            {
                ClearResults();
                ShowPrompt = true;
                StatusMessage = "The selected equipment no longer exists. Please select an item.";
                return;
            }

            Rows.Clear();

            if (!result.IsSuccess)
            {
                IsEmpty = false;
                HeaderText = null;
                LoadError = result.Message;
                return;
            }

            foreach (var record in result.Value!)
            {
                Rows.Add(new BorrowingRow(record));
            }

            // An item without records is an empty state, not an error (BR-19)
            IsEmpty = Rows.Count == 0;
            EmptyMessage = "This item has no borrowing records yet.";
            HeaderText = Rows.Count == 1
                ? $"Borrowing history of {selected.Label} - 1 record found."
                : $"Borrowing history of {selected.Label} - {Rows.Count} records found.";
        }
        finally
        {
            _suppressLoad = false;
            if (version == _loadVersion) IsLoading = false;
        }
    }
}