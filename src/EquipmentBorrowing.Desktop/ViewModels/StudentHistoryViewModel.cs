using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Desktop.Api;

namespace EquipmentBorrowing.Desktop.ViewModels;

// UC-07: select a student and see that student's active and returned borrowings. BR-14, BR-18, BR-19.
public partial class StudentHistoryViewModel : PageViewModelBase
{
    private readonly StudentsApi _studentsApi;
    private readonly BorrowingsApi _borrowingsApi;

    private int _loadVersion;
    private bool _suppressLoad;

    public ObservableCollection<StudentChoice> Students { get; } = new();
    public ObservableCollection<BorrowingRow> Rows { get; } = new();

    [ObservableProperty]
    public partial StudentChoice? SelectedStudent { get; set; }

    // "Select a student" hint, shown while nothing is selected
    [ObservableProperty]
    public partial bool ShowPrompt { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string? EmptyMessage { get; set; }

    [ObservableProperty]
    public partial string? HeaderText { get; set; }

    public StudentHistoryViewModel(StudentsApi studentsApi, BorrowingsApi borrowingsApi)
    {
        _studentsApi = studentsApi;
        _borrowingsApi = borrowingsApi;
    }

    // Parameterless constructor is only used by the XAML designer preview
    public StudentHistoryViewModel()
    {
        _studentsApi = null!;
        _borrowingsApi = null!;
    }

    // Called every time the page is opened: reloads the student list, then the history
    public Task LoadAsync() => ReloadAsync(reloadStudentList: true);

    // Also used by the Retry button
    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync(reloadStudentList: true);

    // Choosing another student replaces the shown records (UC-07 1a)
    partial void OnSelectedStudentChanged(StudentChoice? value)
    {
        if (_suppressLoad || _borrowingsApi is null) return;
        _ = ReloadAsync(reloadStudentList: false);
    }

    private void ClearResults()
    {
        Rows.Clear();
        IsEmpty = false;
        HeaderText = null;
    }

    private async Task ReloadAsync(bool reloadStudentList)
    {
        var version = ++_loadVersion;
        IsLoading = true;
        LoadError = null;
        StatusMessage = null;

        try
        {
            if (reloadStudentList)
            {
                var list = await _studentsApi.SearchAsync(null);

                // A newer load has started; drop this old result
                if (version != _loadVersion) return;

                if (!list.IsSuccess)
                {
                    ClearResults();
                    ShowPrompt = false;
                    LoadError = list.Message;
                    return;
                }

                var previousId = SelectedStudent?.Id;

                _suppressLoad = true;
                Students.Clear();
                foreach (var student in list.Value!)
                {
                    Students.Add(new StudentChoice(student));
                }
                SelectedStudent = previousId.HasValue
                    ? Students.FirstOrDefault(s => s.Id == previousId.Value)
                    : null;
                _suppressLoad = false;

                // The selected student is no longer in the list: explain and ask to choose again
                if (previousId.HasValue && SelectedStudent is null)
                {
                    ClearResults();
                    ShowPrompt = true;
                    StatusMessage = "The previously selected student is no longer available. Please select a student.";
                    return;
                }
            }

            if (SelectedStudent is null)
            {
                ClearResults();
                ShowPrompt = true;
                return;
            }

            ShowPrompt = false;
            var selected = SelectedStudent;
            var result = await _borrowingsApi.StudentHistoryAsync(selected.Id);

            if (version != _loadVersion) return;

            // The server says the student does not exist any more (404)
            if (result.Outcome == ApiOutcome.NotFound)
            {
                ClearResults();
                ShowPrompt = true;
                StatusMessage = "The selected student no longer exists. Please select a student.";
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

            // A student without records is an empty state, not an error (BR-19)
            IsEmpty = Rows.Count == 0;
            EmptyMessage = "This student has no borrowing records yet.";
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