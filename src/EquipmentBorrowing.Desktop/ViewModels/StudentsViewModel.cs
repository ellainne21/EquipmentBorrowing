using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.ViewModels;

// One row of the students list (a display-only copy of a Student).
public class StudentRow
{
    public StudentRow(Student student)
    {
        Id = student.Id;
        StudentNumber = student.StudentNumber;
        FullName = student.Name;
        Contact = student.Contact;
        IsAllowedToBorrow = student.IsAllowedToBorrow;
    }

    public int Id { get; }
    public string StudentNumber { get; }
    public string FullName { get; }
    public string? Contact { get; }
    public bool IsAllowedToBorrow { get; }

    public string ContactText => string.IsNullOrEmpty(Contact) ? "No contact" : Contact;
    public string EligibilityText => IsAllowedToBorrow ? "Eligible" : "Restricted";
}

public partial class StudentsViewModel : PageViewModelBase
{
    private readonly StudentQueryService _query;
    private readonly RegisterStudentService _register;
    private readonly UpdateStudentService _update;
    private readonly ChangeStudentEligibilityService _eligibility;

    private int _loadVersion;          // lets us ignore an older load that finishes late
    private bool _suppressFormFill;    // true while we re-select a row without touching the form

    public ObservableCollection<StudentRow> Students { get; } = new();

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    public partial StudentRow? SelectedStudent { get; set; }

    [ObservableProperty]
    public partial string? FormStudentNumber { get; set; }

    [ObservableProperty]
    public partial string? FormFullName { get; set; }

    [ObservableProperty]
    public partial string? FormContact { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string? EmptyMessage { get; set; }

    public StudentsViewModel(
        StudentQueryService query,
        RegisterStudentService register,
        UpdateStudentService update,
        ChangeStudentEligibilityService eligibility)
    {
        _query = query;
        _register = register;
        _update = update;
        _eligibility = eligibility;
    }

    // Parameterless constructor is only used by the XAML designer preview
    public StudentsViewModel()
    {
        _query = null!;
        _register = null!;
        _update = null!;
        _eligibility = null!;
    }

    // Choosing a row copies its saved values into the form.
    partial void OnSelectedStudentChanged(StudentRow? value)
    {
        if (value is null || _suppressFormFill) return;

        FormStudentNumber = value.StudentNumber;
        FormFullName = value.FullName;
        FormContact = value.Contact;
    }

    public Task LoadAsync() => ReloadAsync(SelectedStudent?.Id, refillForm: false);

    [RelayCommand]
    private Task SearchAsync() => ReloadAsync(SelectedStudent?.Id, refillForm: false);

    [RelayCommand]
    private Task ClearSearchAsync()
    {
        SearchText = null;
        return ReloadAsync(SelectedStudent?.Id, refillForm: false);
    }

    [RelayCommand]
    private void NewStudent()
    {
        SelectedStudent = null;
        FormStudentNumber = null;
        FormFullName = null;
        FormContact = null;
        StatusMessage = null;
    }

    // UC-12: register a new student
    [RelayCommand]
    private async Task RegisterAsync()
    {
        var result = await _register.ExecuteAsync(FormStudentNumber, FormFullName, FormContact);

        if (result.IsSuccess)
        {
            SearchText = null; // make sure the new student is visible in the list
        }

        await ShowResultAsync(result);
    }

    // UC-13: update the selected student's details
    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (SelectedStudent is null)
        {
            StatusMessage = "Select a student from the list first.";
            return;
        }

        var result = await _update.ExecuteAsync(
            SelectedStudent.Id, FormStudentNumber, FormFullName, FormContact);

        await ShowResultAsync(result);
    }

    // UC-14: change borrowing eligibility
    [RelayCommand]
    private Task PermitAsync() => ChangeEligibilityAsync(true);

    [RelayCommand]
    private Task RestrictAsync() => ChangeEligibilityAsync(false);

    private async Task ChangeEligibilityAsync(bool isAllowed)
    {
        if (SelectedStudent is null)
        {
            StatusMessage = "Select a student from the list first.";
            return;
        }

        var result = await _eligibility.ExecuteAsync(SelectedStudent.Id, isAllowed);
        await ShowResultAsync(result);
    }

    private async Task ShowResultAsync(OperationResult result)
    {
        StatusMessage = $"{result.Outcome}: {result.Message}";

        // After a success the form shows the saved values. After a rejection it keeps what the user typed.
        await ReloadAsync(result.EntityId ?? SelectedStudent?.Id, refillForm: result.IsSuccess);
    }

    private async Task ReloadAsync(int? selectId, bool refillForm)
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;

        try
        {
            var result = await _query.SearchAsync(SearchText);

            if (version != _loadVersion) return; // a newer load started; drop this old result

            Students.Clear();

            if (!result.IsSuccess)
            {
                IsEmpty = false;
                LoadError = result.ErrorMessage;
                return;
            }

            foreach (var student in result.Items)
            {
                Students.Add(new StudentRow(student));
            }

            IsEmpty = Students.Count == 0;
            EmptyMessage = string.IsNullOrWhiteSpace(SearchText)
                ? "No students are registered yet."
                : "No students match your search.";

            if (selectId.HasValue)
            {
                _suppressFormFill = !refillForm;
                SelectedStudent = Students.FirstOrDefault(r => r.Id == selectId.Value);
                _suppressFormFill = false;
            }
        }
        finally
        {
            if (version == _loadVersion) IsLoading = false;
        }
    }
}
