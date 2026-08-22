# Campus Equipment Borrowing System

## Part A: System Analysis

### Actors
- **Student**: Expects to borrow available equipment and have borrowing limits enforced.
- **Laboratory Administrator** (Implicit): Expects the system to track equipment status and student borrowing history.

### Use Cases

| Item | Description |
|------|-------------|
| **Use Case** | Borrow Equipment |
| **Primary Actor** | Student |
| **Preconditions** | Student is registered in the system; Equipment exists in the inventory |
| **Main Action** | Student requests to borrow available equipment; System validates student eligibility and equipment availability |
| **Expected Result** | Borrowing record created; Equipment marked as unavailable |
| **Possible Failure** | Student not allowed to borrow; Equipment does not exist; Equipment unavailable; Student reached maximum borrowing limit |

### Domain Concepts

**Student**
- Information: Id, Name, IsAllowedToBorrow
- Rules: Cannot borrow if not allowed
- Not Responsible For: Knowing which equipment is borrowed

**Equipment**
- Information: Id, Name, IsAvailable
- Rules: Can only be borrowed if available
- Not Responsible For: Tracking who borrowed it

**Borrowing**
- Information: Id, StudentId, EquipmentId, DateBorrowed, ExpectedReturnDate, Status
- Rules: Status changes from Active to Returned
- Not Responsible For: Validating student eligibility

---

## Part I: Architecture Explanation

### 1. Solution Structure

**EquipmentBorrowing.Domain**
- Contains core business entities (Student, Equipment, Borrowing, BorrowingStatus)
- Represents the problem domain without technical concerns
- Has NO dependencies on other projects

**EquipmentBorrowing.Application**
- Contains use case implementations (BorrowEquipmentService)
- Defines repository interfaces (IStudentRepository, IEquipmentRepository, IBorrowingRepository)
- Coordinates domain objects to accomplish business operations

**EquipmentBorrowing.Infrastructure**
- Contains technical implementations of repository interfaces
- Currently uses in-memory storage (C# Lists)
- Can be replaced with database implementations without changing other layers

**EquipmentBorrowing.Tests**
- Contains automated tests (structure created, tests can be added later)

**EquipmentBorrowing.Demo**
- Console application demonstrating the system works
- Manually wires up dependencies (Dependency Injection)

### 2. Dependency Direction

Demo / Future UI
      │
      ▼
 Application ──────► Domain
      ▲                  
      │                  
 Infrastructure ──────────┘


**Explanation:**
- Application layer depends on Domain (uses entities)
- Infrastructure layer depends on both Application (implements interfaces) and Domain (uses entities)
- Domain layer has NO dependencies (pure business logic)
- This follows the Dependency Inversion Principle

### 3. Use Case Mapping

**Use Case: Borrow Equipment**

- **Actor**: Student
- **Use Case**: Borrow Equipment
- **Application Service**: BorrowEquipmentService
- **Domain Objects Used**: Student, Equipment, Borrowing, BorrowingStatus
- **Repository Interfaces Used**: IStudentRepository, IEquipmentRepository, IBorrowingRepository
- **Infrastructure Implementations Used**: InMemoryStudentRepository, InMemoryEquipmentRepository, InMemoryBorrowingRepository

### 4. Reflection

**1. Why should the application service depend on a repository interface instead of directly depending on a database implementation?**

The application service should depend on interfaces to achieve **separation of concerns** and follow the **Dependency Inversion Principle**. This makes the code:
- **Testable**: We can use in-memory repositories for testing without a database
- **Flexible**: We can swap implementations (SQLite, PostgreSQL, API) without changing business logic
- **Maintainable**: Changes to data storage don't affect business rules

**2. Which parts of your current solution could remain unchanged if SQLite were added later?**

- **Domain layer**: All entities (Student, Equipment, Borrowing) would remain exactly the same
- **Application layer**: BorrowEquipmentService and repository interfaces would NOT change
- Only the **Infrastructure layer** would change (we'd add SqliteRepository implementations)

**3. Which project would eventually contain Avalonia Views?**

A new project (e.g., `EquipmentBorrowing.UI` or `EquipmentBorrowing.Desktop`) would contain Avalonia Views. This project would depend on the Application layer to call use cases.

**4. Should an Avalonia button directly execute database queries? Why or why not?**

**NO.** An Avalonia button should NOT directly execute database queries because:
- It violates **separation of concerns** (UI mixing with data access)
- It makes the code **untestable** (UI code is hard to test automatically)
- It creates **tight coupling** (changing database changes the UI)
- The button should call the **Application Service** (BorrowEquipmentService), which coordinates the operation

**5. What part of your implementation represents the actual business operation requested by the actor?**

The **BorrowEquipmentService.ExecuteAsync()** method in the Application layer represents the actual business operation. It:
- Validates all business rules (student allowed, equipment available, borrowing limits)
- Coordinates domain objects and repositories
- Creates the borrowing record when all conditions are met
- Returns clear success/failure messages