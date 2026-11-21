using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Desktop.Api;

namespace EquipmentBorrowing.Desktop.ViewModels;

// One row of the equipment catalog. The tick box chooses it for checkout.
public partial class EquipmentChoice : ObservableObject
{
    public EquipmentChoice(EquipmentDto equipment)
    {
        Id = equipment.Id;
        Identifier = equipment.EquipmentCode;
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

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}

// One entry of the student drop-down.
public class StudentChoice
{
    public StudentChoice(StudentDto student)
    {
        Id = student.Id;
        Label = $"{student.StudentNumber} - {student.FullName}" + (student.IsEligible ? "" : " (restricted)");
    }

    public int Id { get; }
    public string Label { get; }
}

public partial class EquipmentViewModel : PageViewModelBase
{
    private readonly EquipmentApi _equipmentApi;
    private readonly StudentsApi _studentsApi;
    private readonly BorrowingsApi _borrowingsApi;

    private int _loadVersion;

    public ObservableCollection<EquipmentChoice> EquipmentList { get; } = new();
    public ObservableCollection<StudentChoice> Students { get; } = new();

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    public partial bool AvailableOnly { get; set; }

    [ObservableProperty]
    public partial StudentChoice? SelectedStudent { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset ExpectedReturnDate { get; set; } = DateTimeOffset.Now.AddDays(7);

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string? EmptyMessage { get; set; }

    public EquipmentViewModel(EquipmentApi equipmentApi, StudentsApi studentsApi, BorrowingsApi borrowingsApi)
    {
        _equipmentApi = equipmentApi;
        _studentsApi = studentsApi;
        _borrowingsApi = borrowingsApi;

        _ = LoadAsync(); // the Equipment page is the first page shown
    }

    // Parameterless constructor is only used by the XAML designer preview
    public EquipmentViewModel()
    {
        _equipmentApi = null!;
        _studentsApi = null!;
        _borrowingsApi = null!;
    }

    // Ticking "available only" refreshes the list straight away (UC-02).
    partial void OnAvailableOnlyChanged(bool value)
    {
        if (_equipmentApi is null) return;
        _ = ReloadAsync(clearChecks: false);
    }

    public Task LoadAsync() => ReloadAsync(clearChecks: false);

    [RelayCommand]
    private Task SearchAsync() => ReloadAsync(clearChecks: false);

    [RelayCommand]
    private Task ClearSearchAsync()
    {
        SearchText = null;
        return ReloadAsync(clearChecks: false);
    }

    // UC-03 (one ticked item, API-10) and UC-06 (two or more ticked items, API-11, all or nothing)
    [RelayCommand]
    private async Task CheckOutAsync()
    {
        // Presentation checks only. The real rules live on the server.
        if (SelectedStudent is null)
        {
            StatusMessage = "Select a student first.";
            return;
        }

        var ids = EquipmentList.Where(e => e.IsChecked).Select(e => e.Id).ToList();
        if (ids.Count == 0)
        {
            StatusMessage = "Tick at least one item to check out.";
            return;
        }

        var returnDate = DateOnly.FromDateTime(ExpectedReturnDate.Date);
        IsLoading = true;

        bool success;
        if (ids.Count == 1)
        {
            var result = await _borrowingsApi.BorrowAsync(SelectedStudent.Id, ids[0], returnDate);
            success = result.IsSuccess;
            StatusMessage = success ? "Checked out 1 item." : Describe(result.Outcome, result.Message);
        }
        else
        {
            var result = await _borrowingsApi.CheckoutAsync(SelectedStudent.Id, ids, returnDate);
            success = result.IsSuccess;
            StatusMessage = success
                ? $"Checked out {ids.Count} items. They were saved together."
                : Describe(result.Outcome, result.Message);
        }

        // Always refresh from the API, so the screen shows what is really stored now
        // (after a success, a conflict, or an unknown outcome).
        await ReloadAsync(clearChecks: success);
    }

    private async Task ReloadAsync(bool clearChecks)
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;

        // Remember the user's choices so a refresh does not throw them away
        var checkedIds = clearChecks
            ? new HashSet<int>()
            : EquipmentList.Where(e => e.IsChecked).Select(e => e.Id).ToHashSet();
        var studentId = SelectedStudent?.Id;

        try
        {
            var equipment = await _equipmentApi.SearchAsync(SearchText, AvailableOnly);
            var students = await _studentsApi.SearchAsync(null);

            if (version != _loadVersion) return; // a newer load started; drop this old result

            EquipmentList.Clear();
            Students.Clear();

            if (!equipment.IsSuccess || !students.IsSuccess)
            {
                IsEmpty = false;
                LoadError = !equipment.IsSuccess ? equipment.Message : students.Message;
                return;
            }

            foreach (var student in students.Value!)
            {
                Students.Add(new StudentChoice(student));
            }

            foreach (var item in equipment.Value!)
            {
                EquipmentList.Add(new EquipmentChoice(item) { IsChecked = checkedIds.Contains(item.Id) });
            }

            IsEmpty = EquipmentList.Count == 0;
            EmptyMessage = (string.IsNullOrWhiteSpace(SearchText), AvailableOnly) switch
            {
                (true, false) => "No equipment is registered yet.",
                (true, true) => "No equipment is available right now.",
                _ => "No equipment matches your search."
            };

            if (studentId.HasValue)
            {
                SelectedStudent = Students.FirstOrDefault(s => s.Id == studentId.Value);
            }
        }
        finally
        {
            if (version == _loadVersion) IsLoading = false;
        }
    }
}