using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class BorrowEquipmentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IBorrowingRepository _borrowingRepository;

    private const int MaxActiveBorrowings = 3;

    // Dependency Injection via Constructor (Part F)
    public BorrowEquipmentService(
        IStudentRepository studentRepository,
        IEquipmentRepository equipmentRepository,
        IBorrowingRepository borrowingRepository)
    {
        _studentRepository = studentRepository;
        _equipmentRepository = equipmentRepository;
        _borrowingRepository = borrowingRepository;
    }

    public async Task<string> ExecuteAsync(
        int studentId, 
        int equipmentId, 
        CancellationToken cancellationToken = default)
    {
        // 1. Does the student exist?
        var student = await _studentRepository.GetByIdAsync(studentId, cancellationToken);
        if (student == null)
        {
            return "Failure: Student does not exist.";
        }

        // 2. Is the student allowed to borrow?
        if (!student.IsAllowedToBorrow)
        {
            return "Failure: Student is not allowed to borrow.";
        }

        // 3. Does the equipment exist?
        var equipment = await _equipmentRepository.GetByIdAsync(equipmentId, cancellationToken);
        if (equipment == null)
        {
            return "Failure: Equipment does not exist.";
        }

        // 4. Is the equipment currently available?
        if (!equipment.IsAvailable)
        {
            return "Failure: Equipment is currently unavailable.";
        }

        // 5. Has the student reached the maximum number of active borrowings?
        var activeCount = await _borrowingRepository.GetActiveBorrowingsCountByStudentIdAsync(studentId, cancellationToken);
        if (activeCount >= MaxActiveBorrowings)
        {
            return $"Failure: Student has reached the maximum limit of {MaxActiveBorrowings} active borrowings.";
        }

        // 6. All validations passed - create the borrowing record
        var borrowing = new Borrowing(
            id: new Random().Next(1000, 9999), // Simple ID generation
            studentId: studentId,
            equipmentId: equipmentId,
            expectedReturnDate: DateTime.Now.AddDays(7)
        );

        // Save the borrowing
        await _borrowingRepository.AddAsync(borrowing, cancellationToken);

        // Mark equipment as unavailable
        equipment.MarkAsBorrowed();
        await _equipmentRepository.UpdateAsync(equipment, cancellationToken);

        return $"Success: Borrowing created for Student {student.Name}. Expected return: {borrowing.ExpectedReturnDate.ToShortDateString()}";
    }
}