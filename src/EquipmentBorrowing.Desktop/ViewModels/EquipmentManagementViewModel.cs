using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.ViewModels;

// One row of the equipment list (a display-only copy of an Equipment item).
public class EquipmentRow
{
    public EquipmentRow(Equipment equipment)
    {
        Id = equipment.Id;
        Identifier = equipment.Identifier;
        Name = equipment.Name;
        Description = equipment.Description;
        IsAvailable = equipment.IsAvailable;
    }

    public int Id { get; }
    public string Identifier { get; }
    public string Name { get; }
    public string? Description { get; }
    public bool IsAvailable { get; }

    public string DescriptionText => string.IsNullOrEmpty(Description) ? "No description" : Description;
    public string AvailabilityText => IsAvailable ? "Available" : "Unavailable";
}

public partial class EquipmentManagementViewModel : PageViewModelBase
{
    private readonly EquipmentQueryService _query;
    private readonly RegisterEquipmentService _register;
    private readonly UpdateEquipmentService _update;

    private int _loadVersion;
    private bool _suppressFormFill;

    public ObservableCollection<EquipmentRow> EquipmentList { get; } = new();

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    public partial bool AvailableOnly { get; set; }

    [ObservableProperty]
    public partial EquipmentRow? SelectedEquipment { get; set; }

    [ObservableProperty]
    public partial string? FormIdentifier { get; set; }

    [ObservableProperty]
    public partial string? FormName { get; set; }

    [ObservableProperty]
    public partial string? FormDescription { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string? EmptyMessage { get; set; }

    public EquipmentManagementViewModel(
        EquipmentQueryService query,
        RegisterEquipmentService register,
        UpdateEquipmentService update)
    {
        _query = query;
        _register = register;
        _update = update;
    }

    // Parameterless constructor is only used by the XAML designer preview
    public EquipmentManagementViewModel()
    {
        _query = null!;
        _register = null!;
        _update = null!;
    }

    partial void OnSelectedEquipmentChanged(EquipmentRow? value)
    {
        if (value is null || _suppressFormFill) return;

        FormIdentifier = value.Identifier;
        FormName = value.Name;
        FormDescription = value.Description;
    }

    // Ticking or unticking "Available only" refreshes the list straight away (UC-02).
    partial void OnAvailableOnlyChanged(bool value)
    {
        _ = ReloadAsync(SelectedEquipment?.Id, refillForm: false);
    }

    public Task LoadAsync() => ReloadAsync(SelectedEquipment?.Id, refillForm: false);

    [RelayCommand]
    private Task SearchAsync() => ReloadAsync(SelectedEquipment?.Id, refillForm: false);

    [RelayCommand]
    private Task ClearSearchAsync()
    {
        SearchText = null;
        return ReloadAsync(SelectedEquipment?.Id, refillForm: false);
    }

    [RelayCommand]
    private void NewEquipment()
    {
        SelectedEquipment = null;
        FormIdentifier = null;
        FormName = null;
        FormDescription = null;
        StatusMessage = null;
    }

    // UC-15: register new equipment
    [RelayCommand]
    private async Task RegisterAsync()
    {
        var result = await _register.ExecuteAsync(FormIdentifier, FormName, FormDescription);

        if (result.IsSuccess)
        {
            SearchText = null;
            AvailableOnly = false; // new equipment is available, but clear filters so it is easy to see
        }

        await ShowResultAsync(result);
    }

    // UC-16: update the selected equipment's descriptive details (never its availability)
    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (SelectedEquipment is null)
        {
            StatusMessage = "Select an item from the list first.";
            return;
        }

        var result = await _update.ExecuteAsync(
            SelectedEquipment.Id, FormIdentifier, FormName, FormDescription);

        await ShowResultAsync(result);
    }

    private async Task ShowResultAsync(OperationResult result)
    {
        StatusMessage = $"{result.Outcome}: {result.Message}";
        await ReloadAsync(result.EntityId ?? SelectedEquipment?.Id, refillForm: result.IsSuccess);
    }

    private async Task ReloadAsync(int? selectId, bool refillForm)
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;

        try
        {
            var result = await _query.SearchAsync(SearchText, AvailableOnly);

            if (version != _loadVersion) return;

            EquipmentList.Clear();

            if (!result.IsSuccess)
            {
                IsEmpty = false;
                LoadError = result.ErrorMessage;
                return;
            }

            foreach (var item in result.Items)
            {
                EquipmentList.Add(new EquipmentRow(item));
            }

            IsEmpty = EquipmentList.Count == 0;
            EmptyMessage = (string.IsNullOrWhiteSpace(SearchText), AvailableOnly) switch
            {
                (true, false) => "No equipment is registered yet.",
                (true, true) => "No equipment is available right now.",
                _ => "No equipment matches your search."
            };

            if (selectId.HasValue)
            {
                _suppressFormFill = !refillForm;
                SelectedEquipment = EquipmentList.FirstOrDefault(r => r.Id == selectId.Value);
                _suppressFormFill = false;
            }
        }
        finally
        {
            if (version == _loadVersion) IsLoading = false;
        }
    }
}
