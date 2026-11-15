using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly EquipmentViewModel _equipmentViewModel;
    private readonly BorrowingsViewModel _borrowingsViewModel;
    private readonly StudentsViewModel _studentsViewModel;
    private readonly EquipmentManagementViewModel _equipmentManagementViewModel;

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    public MainViewModel(
        EquipmentViewModel equipmentViewModel,
        BorrowingsViewModel borrowingsViewModel,
        StudentsViewModel studentsViewModel,
        EquipmentManagementViewModel equipmentManagementViewModel)
    {
        _equipmentViewModel = equipmentViewModel;
        _borrowingsViewModel = borrowingsViewModel;
        _studentsViewModel = studentsViewModel;
        _equipmentManagementViewModel = equipmentManagementViewModel;

        // Default page shown when the app starts
        CurrentPage = _equipmentViewModel;
    }

    // Parameterless constructor required only for the XAML Previewer's Design.DataContext
    public MainViewModel() : this(
        new EquipmentViewModel(),
        new BorrowingsViewModel(),
        new StudentsViewModel(),
        new EquipmentManagementViewModel())
    {
    }

    [RelayCommand]
    private async Task ShowEquipment()
    {
        await _equipmentViewModel.LoadAsync();
        CurrentPage = _equipmentViewModel;
    }

    [RelayCommand]
    private async Task ShowBorrowings()
    {
        await _borrowingsViewModel.LoadAsync();
        CurrentPage = _borrowingsViewModel;
    }

    // New pages switch first and then load, so the user sees the loading state.
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
