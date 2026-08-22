using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Repositories;

Console.WriteLine("=== Campus Equipment Borrowing System Demo ===\n");

// ========== SETUP: Create In-Memory Repositories ==========
var studentRepo = new InMemoryStudentRepository();
var equipmentRepo = new InMemoryEquipmentRepository();
var borrowingRepo = new InMemoryBorrowingRepository();

// ========== SEED: Add Test Data ==========
// Add students
studentRepo.Seed(new Student(1, "Alice Santos", true));   // Allowed to borrow
studentRepo.Seed(new Student(2, "Bob Reyes", false));     // NOT allowed to borrow

// Add equipment
equipmentRepo.Seed(new Equipment(101, "Laptop Dell XPS 15"));
equipmentRepo.Seed(new Equipment(102, "Projector Epson"));
equipmentRepo.Seed(new Equipment(103, "Arduino Kit"));

// ========== DEPENDENCY INJECTION: Wire up the service ==========
// Manually inject the repository implementations into the service
var borrowService = new BorrowEquipmentService(
    studentRepo,
    equipmentRepo,
    borrowingRepo
);

// ========== DEMO 1: SUCCESS CASE ==========
Console.WriteLine("[TEST 1] Alice (Allowed) borrows Laptop (Available)");
Console.WriteLine("Expected: SUCCESS");
var result1 = await borrowService.ExecuteAsync(
    studentId: 1, 
    equipmentId: 101
);
Console.WriteLine($"Result: {result1}\n");

// ========== DEMO 2: FAILURE CASE - Student Not Allowed ==========
Console.WriteLine("[TEST 2] Bob (NOT Allowed) borrows Projector");
Console.WriteLine("Expected: FAILURE - Student not allowed");
var result2 = await borrowService.ExecuteAsync(
    studentId: 2, 
    equipmentId: 102
);
Console.WriteLine($"Result: {result2}\n");

// ========== DEMO 3: FAILURE CASE - Equipment Unavailable ==========
Console.WriteLine("[TEST 3] Alice tries to borrow Laptop again (Already borrowed)");
Console.WriteLine("Expected: FAILURE - Equipment unavailable");
var result3 = await borrowService.ExecuteAsync(
    studentId: 1, 
    equipmentId: 101
);
Console.WriteLine($"Result: {result3}\n");

Console.WriteLine("=== Demo Complete ===");Console.WriteLine("Hello, World!");
