using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class EquipmentViewModel : ViewModelBase
{
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly BorrowEquipmentService _borrowEquipmentService;

    public ObservableCollection<Equipment> EquipmentList { get; } = new();
    public ObservableCollection<Student> Students { get; } = new();

    [ObservableProperty]
    public partial Equipment? SelectedEquipment { get; set; }

    [ObservableProperty]
    public partial Student? SelectedStudent { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset ExpectedReturnDate { get; set; } = DateTimeOffset.Now.AddDays(7);

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public EquipmentViewModel(
        IEquipmentRepository equipmentRepository,
        IStudentRepository studentRepository,
        BorrowEquipmentService borrowEquipmentService)
    {
        _equipmentRepository = equipmentRepository;
        _studentRepository = studentRepository;
        _borrowEquipmentService = borrowEquipmentService;

        _ = LoadAsync();
    }

    // Parameterless constructor is only used by the XAML designer preview
    public EquipmentViewModel()
    {
        _equipmentRepository = null!;
        _studentRepository = null!;
        _borrowEquipmentService = null!;
    }

    public async Task LoadAsync()
    {
        EquipmentList.Clear();
        foreach (var equipment in await _equipmentRepository.GetAllAsync())
        {
            EquipmentList.Add(equipment);
        }

        Students.Clear();
        foreach (var student in await _studentRepository.GetAllAsync())
        {
            Students.Add(student);
        }
    }

    [RelayCommand]
    private async Task BorrowAsync()
    {
        // Presentation validation only - no business rules here
        if (SelectedStudent is null)
        {
            StatusMessage = "Please select a student.";
            return;
        }

        if (SelectedEquipment is null)
        {
            StatusMessage = "Please select equipment.";
            return;
        }

        if (ExpectedReturnDate.Date <= DateTimeOffset.Now.Date)
        {
            StatusMessage = "Expected return date must be in the future.";
            return;
        }

        // The application service decides whether the borrow is actually valid
        var result = await _borrowEquipmentService.ExecuteAsync(
            SelectedStudent.Id,
            SelectedEquipment.Id,
            ExpectedReturnDate.DateTime);

        StatusMessage = result;

        // Refresh so availability reflects the change
        await LoadAsync();
    }
}