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

---

---

## Laboratory Activity 2: Avalonia UI and MVVM

### 1. Desktop Project

`EquipmentBorrowing.Desktop` is the part of the app that the user actually sees and clicks on. Its job is to:
- Show the list of equipment and active borrowings
- Let the user pick a student, equipment, and return date
- Remember what the user has selected and which page they're on
- Call the existing services (`BorrowEquipmentService`, `ReturnEquipmentService`) when the user clicks a button
- Show a message telling the user if something worked or failed

It uses `EquipmentBorrowing.Application` (to call the services) and `EquipmentBorrowing.Infrastructure` (to get the actual data). It does not contain any of the borrowing rules itself — those all still live in the Domain and Application layers from Lab 1. The Desktop project just shows things and passes user actions along.

### 2. Updated Architecture

```
Avalonia View (MainWindow, EquipmentView, BorrowingsView)
        |
        | Binding / Command
        v
ViewModel (MainViewModel, EquipmentViewModel, BorrowingsViewModel)
        |
        | Application Operation
        v
Application Service (BorrowEquipmentService, ReturnEquipmentService)
        |
        +----------> Domain (Student, Equipment, Borrowing)
        |
        v
Repository Interface (IStudentRepository, IEquipmentRepository, IBorrowingRepository)
        ^
        |
Infrastructure Implementation (InMemoryStudentRepository, InMemoryEquipmentRepository, InMemoryBorrowingRepository)
```

The Domain and Application layers from Lab 1 didn't really change. We just added the Desktop project on top of them.

### 3. Borrow Equipment Flow

1. The user picks a student, picks equipment, and picks a return date in `EquipmentView`.
2. The user clicks **Borrow Equipment**, which runs `EquipmentViewModel.BorrowAsync()`.
3. `BorrowAsync()` only checks simple things first — did the user actually pick a student, pick equipment, and pick a real date.
4. If that's fine, it calls `_borrowEquipmentService.ExecuteAsync(studentId, equipmentId, expectedReturnDate)`.
5. `BorrowEquipmentService` (the same one from Lab 1) checks the real rules: does the student exist, are they allowed to borrow, does the equipment exist, is it available, is the date okay, and has the student already borrowed too many things.
6. If everything checks out, it creates the borrowing, marks the equipment as borrowed, and sends back a success message.
7. That message shows up on screen in `StatusMessage`.
8. The equipment list refreshes so the user sees the updated availability right away.

### 4. Return Equipment Flow

1. The user goes to `BorrowingsView` and clicks on a borrowing in the list.
2. The user clicks **Return Equipment**, which runs `BorrowingsViewModel.ReturnAsync()`.
3. `ReturnAsync()` just checks that something was actually selected.
4. It calls `_returnEquipmentService.ExecuteAsync(borrowingId)`.
5. `ReturnEquipmentService` finds the borrowing, checks it wasn't already returned, marks it as returned, and marks the equipment as available again.
6. The result message is shown to the user.
7. The list refreshes, so the returned item disappears, and the equipment shows as available again if you check the Equipment page.

### 5. Architectural Reflection

**1. Why should the View not call a repository directly?**
The View is only supposed to show things and take input. If it talked to the repository directly, it could skip all the rules (like checking if a student is allowed to borrow), since repositories don't know about those rules — only the Application layer does.

**2. Why should business rules not be implemented in the ViewModel?**
If we wrote the rules again inside the ViewModel, we'd have the same rule in two places. If we ever needed to change a rule, we'd have to remember to change it in both places, and it's easy to forget one. Keeping the rule in only one place (the Application layer) avoids that problem.

**3. What is the responsibility of the ViewModel?**
The ViewModel keeps track of what's on screen — like the list of equipment, what's selected, and any messages to show. It also has the commands (like "Borrow" or "Return") that the buttons use. It checks simple stuff like "did the user pick something," but it doesn't decide if a borrow is actually allowed — it just asks the service.

**4. Why can the existing Application layer work without knowing that Avalonia is being used?**
Because `BorrowEquipmentService` and `ReturnEquipmentService` only depend on the repository interfaces and the Domain classes — nothing about Avalonia. That means the same service code could be used by a totally different interface (like our console Demo app) without any changes.

**5. What advantage is gained from registering dependencies in one composition point?**
Setting up everything in one place (`App.axaml.cs`) makes it easy to see how the whole app is wired together. If something needs to change — like using a different repository — we only need to update it in one spot, instead of hunting through the whole codebase.

**6. If the in-memory repository were replaced by SQLite later, which parts of the current interface should remain largely unchanged?**
Everything except the Infrastructure layer would stay the same — the Domain classes, the Application services, and the whole Desktop project (Views and ViewModels) wouldn't need to change at all. We would only need to write new SQLite versions of the repositories and swap them in during setup.
