using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Services;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly EquipmentViewModel _equipmentViewModel;
    private readonly BorrowingsViewModel _borrowingsViewModel;

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    public MainViewModel(EquipmentViewModel equipmentViewModel, BorrowingsViewModel borrowingsViewModel)
    {
        _equipmentViewModel = equipmentViewModel;
        _borrowingsViewModel = borrowingsViewModel;

        // Default page shown when the app starts
        CurrentPage = _equipmentViewModel;
    }

    // Parameterless constructor required only for the XAML Previewer's Design.DataContext
    public MainViewModel() : this(new EquipmentViewModel(), new BorrowingsViewModel())
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
}