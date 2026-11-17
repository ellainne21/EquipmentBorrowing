using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly EquipmentViewModel _equipmentViewModel;
    private readonly BorrowingsViewModel _borrowingsViewModel;
    private readonly BorrowingSearchViewModel _borrowingSearchViewModel;
    private readonly StudentsViewModel _studentsViewModel;
    private readonly EquipmentManagementViewModel _equipmentManagementViewModel;

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    public MainViewModel(
        EquipmentViewModel equipmentViewModel,
        BorrowingsViewModel borrowingsViewModel,
        BorrowingSearchViewModel borrowingSearchViewModel,
        StudentsViewModel studentsViewModel,
        EquipmentManagementViewModel equipmentManagementViewModel)
    {
        _equipmentViewModel = equipmentViewModel;
        _borrowingsViewModel = borrowingsViewModel;
        _borrowingSearchViewModel = borrowingSearchViewModel;
        _studentsViewModel = studentsViewModel;
        _equipmentManagementViewModel = equipmentManagementViewModel;

        // Default page shown when the app starts
        CurrentPage = _equipmentViewModel;
    }

    // Parameterless constructor required only for the XAML Previewer's Design.DataContext
    public MainViewModel() : this(
        new EquipmentViewModel(),
        new BorrowingsViewModel(),
        new BorrowingSearchViewModel(),
        new StudentsViewModel(),
        new EquipmentManagementViewModel())
    {
    }

    // Every page switches first and then loads, so the user sees the loading state.
    [RelayCommand]
    private async Task ShowEquipment()
    {
        CurrentPage = _equipmentViewModel;
        await _equipmentViewModel.LoadAsync();
    }

    [RelayCommand]
    private async Task ShowBorrowings()
    {
        CurrentPage = _borrowingsViewModel;
        await _borrowingsViewModel.LoadAsync();
    }

    [RelayCommand]
    private async Task ShowBorrowingSearch()
    {
        CurrentPage = _borrowingSearchViewModel;
        await _borrowingSearchViewModel.LoadAsync();
    }

    [RelayCommand]
    private async Task ShowStudents()
    {
        CurrentPage = _studentsViewModel;
        await _studentsViewModel.LoadAsync();
    }

    [RelayCommand]
    private async Task ShowEquipmentManagement()
    {
        CurrentPage = _equipmentManagementViewModel;
        await _equipmentManagementViewModel.LoadAsync();
    }
}
