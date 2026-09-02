using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class BorrowingsViewModel : ViewModelBase
{
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly ReturnEquipmentService _returnEquipmentService;

    public ObservableCollection<Borrowing> ActiveBorrowings { get; } = new();

    [ObservableProperty]
    public partial Borrowing? SelectedBorrowing { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public BorrowingsViewModel(
        IBorrowingRepository borrowingRepository,
        ReturnEquipmentService returnEquipmentService)
    {
        _borrowingRepository = borrowingRepository;
        _returnEquipmentService = returnEquipmentService;

        _ = LoadAsync();
    }

    // Parameterless constructor is only used by the XAML designer preview
    public BorrowingsViewModel()
    {
        _borrowingRepository = null!;
        _returnEquipmentService = null!;
    }

    public async Task LoadAsync()
    {
        ActiveBorrowings.Clear();
        foreach (var borrowing in await _borrowingRepository.GetActiveBorrowingsAsync())
        {
            ActiveBorrowings.Add(borrowing);
        }
    }

    [RelayCommand]
    private async Task ReturnAsync()
    {
        if (SelectedBorrowing is null)
        {
            StatusMessage = "Please select a borrowing to return.";
            return;
        }

        var result = await _returnEquipmentService.ExecuteAsync(SelectedBorrowing.Id);
        StatusMessage = result;

        await LoadAsync();
    }
}