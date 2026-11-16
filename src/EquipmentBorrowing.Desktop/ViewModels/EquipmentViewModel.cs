using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.ViewModels;

// One row of the equipment catalog. The tick box chooses it for checkout.
public partial class EquipmentChoice : ObservableObject
{
    public EquipmentChoice(Equipment equipment)
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

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}

// One entry of the student drop-down.
public class StudentChoice
{
    public StudentChoice(Student student)
    {
        Id = student.Id;
        Label = $"{student.StudentNumber} - {student.Name}" + (student.IsAllowedToBorrow ? "" : " (restricted)");
    }

    public int Id { get; }
    public string Label { get; }
}

public partial class EquipmentViewModel : PageViewModelBase
{
    private readonly EquipmentQueryService _equipmentQuery;
    private readonly StudentQueryService _studentQuery;
    private readonly BorrowEquipmentService _borrowOne;
    private readonly CheckOutMultipleEquipmentService _borrowMany;

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

    public EquipmentViewModel(
        EquipmentQueryService equipmentQuery,
        StudentQueryService studentQuery,
        BorrowEquipmentService borrowOne,
        CheckOutMultipleEquipmentService borrowMany)
    {
        _equipmentQuery = equipmentQuery;
        _studentQuery = studentQuery;
        _borrowOne = borrowOne;
        _borrowMany = borrowMany;

        _ = LoadAsync(); // the Equipment page is the first page shown
    }

    // Parameterless constructor is only used by the XAML designer preview
    public EquipmentViewModel()
    {
        _equipmentQuery = null!;
        _studentQuery = null!;
        _borrowOne = null!;
        _borrowMany = null!;
    }

    // Ticking "available only" refreshes the list straight away (UC-02).
    partial void OnAvailableOnlyChanged(bool value)
    {
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

    // UC-03 (one ticked item) and UC-06 (two or more ticked items)
    [RelayCommand]
    private async Task CheckOutAsync()
    {
        // Presentation checks only. The real rules live in the application services.
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

        var returnDate = ExpectedReturnDate.Date;

        var result = ids.Count == 1
            ? await _borrowOne.ExecuteAsync(SelectedStudent.Id, ids[0], returnDate)
            : await _borrowMany.ExecuteAsync(SelectedStudent.Id, ids, returnDate);

        StatusMessage = $"{result.Outcome}: {result.Message}";

        // Always refresh, so the screen shows what is really stored now (also after a conflict).
        await ReloadAsync(clearChecks: result.IsSuccess);
    }

    private async Task ReloadAsync(bool clearChecks)
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;

        // Remember the user's choices so a refresh does not throw them away
        var checkedIds = clearChecks
            ? new System.Collections.Generic.HashSet<int>()
            : EquipmentList.Where(e => e.IsChecked).Select(e => e.Id).ToHashSet();
        var studentId = SelectedStudent?.Id;

        try
        {
            var equipment = await _equipmentQuery.SearchAsync(SearchText, AvailableOnly);
            var students = await _studentQuery.SearchAsync(null);

            if (version != _loadVersion) return; // a newer load started; drop this old result

            EquipmentList.Clear();
            Students.Clear();

            if (!equipment.IsSuccess || !students.IsSuccess)
            {
                IsEmpty = false;
                LoadError = equipment.ErrorMessage ?? students.ErrorMessage;
                return;
            }

            foreach (var student in students.Items)
            {
                Students.Add(new StudentChoice(student));
            }

            foreach (var item in equipment.Items)
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
