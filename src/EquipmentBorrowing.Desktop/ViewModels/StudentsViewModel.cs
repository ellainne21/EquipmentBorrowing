using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Desktop.Api;

namespace EquipmentBorrowing.Desktop.ViewModels;

// One row of the students list (a display-only copy of a StudentDto from the API).
public class StudentRow
{
    public StudentRow(StudentDto student)
    {
        Id = student.Id;
        StudentNumber = student.StudentNumber;
        FullName = student.FullName;
        Contact = student.Contact;
        IsAllowedToBorrow = student.IsEligible;
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
    private readonly StudentsApi _api;

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

    public StudentsViewModel(StudentsApi api)
    {
        _api = api;
    }

    // Parameterless constructor is only used by the XAML designer preview
    public StudentsViewModel()
    {
        _api = null!;
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

    // UC-12: register a new student (API-04)
    [RelayCommand]
    private async Task RegisterAsync()
    {
        IsLoading = true;
        var result = await _api.CreateAsync(FormStudentNumber, FormFullName, FormContact);

        if (result.IsSuccess)
        {
            SearchText = null; // make sure the new student is visible in the list
        }

        await ShowResultAsync(result, "Student registered.");
    }

    // UC-13: update the selected student's details (API-05)
    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (SelectedStudent is null)
        {
            StatusMessage = "Select a student from the list first.";
            return;
        }

        IsLoading = true;
        var result = await _api.UpdateAsync(
            SelectedStudent.Id, FormStudentNumber, FormFullName, FormContact);

        await ShowResultAsync(result, "Student updated.");
    }

    // UC-14: change borrowing eligibility (API-06)
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

        IsLoading = true;
        var result = await _api.SetEligibilityAsync(SelectedStudent.Id, isAllowed);
        await ShowResultAsync(result, isAllowed ? "Student is now eligible to borrow." : "Student is now restricted.");
    }

    private async Task ShowResultAsync(ApiResult<StudentDto> result, string successMessage)
    {
        StatusMessage = result.IsSuccess ? successMessage : result.Message;

        // After a success the form shows the saved values. After a rejection it keeps what the user typed.
        // The list is always refreshed from the API, so stale data is corrected.
        await ReloadAsync(result.Value?.Id ?? SelectedStudent?.Id, refillForm: result.IsSuccess);
    }

    private async Task ReloadAsync(int? selectId, bool refillForm)
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;

        try
        {
            var result = await _api.SearchAsync(SearchText);

            if (version != _loadVersion) return; // a newer load started; drop this old result

            if (!result.IsSuccess)
            {
                // Keep the list on screen and say what went wrong (connection problem or server error).
                IsEmpty = false;
                LoadError = result.Message;
                return;
            }

            Students.Clear();

            foreach (var student in result.Value!)
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