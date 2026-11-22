# Lab 5 Traceability Matrix

Pair: Ellainne G. Cabasag (2023304057) and Floramay S. Balofinos (2024302506)

This matrix connects each Lab 4 use case to its Lab 5 endpoints, the business rules, the code that implements it, and the evidence. It uses the use-case, rule and test numbers of the supplied API and Acceptance Reference. Evidence files are in `docs/lab5/evidence/` (`T0x-requests.txt` request logs, and pgAdmin screenshots named `T0x-*`).

## 1. Use case to API to code to evidence

| Use case | Rules | API | Tests | Implementation reference | Evidence |
| --- | --- | --- | --- | --- | --- |
| UC-01 View Equipment Catalog | BR-05 to 07, 10, 18, 19 | API-01 | T01, T07 | `EquipmentController.List`; `EquipmentQueryService.SearchAsync`; desktop `EquipmentViewModel` | `T01-requests.txt`; `T07-*` (Equipment page) |
| UC-02 Find Available Equipment | BR-10, 18, 19 | API-01 (`available`, `search`) | T01, T07 | `EquipmentController.List`; `EquipmentQueryService.SearchAsync`; `EquipmentViewModel` | `T01-requests.txt`; `T07-*` |
| UC-03 Borrow Equipment | BR-08 to 11, 14 to 16, 18, 19 | API-10, API-13 | T04, T07 | `BorrowingsController.Create` and `Get`; `BorrowEquipmentService`; `CheckoutTransaction`; `EfBorrowingUnitOfWork`; `EquipmentViewModel.CheckOutAsync` | `T04R-requests.txt`; `T04R-*` readbacks; `T07-*` |
| UC-04 View Active Borrowings | BR-14, 18, 19 | API-14 | T01, T06, T07 | `BorrowingsController.List`; `BorrowingQueryService.SearchAsync`; `EfBorrowingRepository.SearchAsync`; `BorrowingsViewModel` | `T06-requests.txt`; `T07-06-*` |
| UC-05 Return Equipment | BR-10, 13 to 16, 18, 19 | API-12, API-13 | T04, T07 | `BorrowingsController.Return` and `Get`; `ReturnEquipmentService`; `EfBorrowingUnitOfWork`; `BorrowingsViewModel.ReturnAsync` | `T04R-requests.txt`; `T04R-*`; `T07-07-*` |
| UC-06 Check Out Multiple Equipment Items | BR-08 to 12, 14 to 16, 18, 19 | API-11 | T05, T07 | `BorrowingsController.Checkout`; `CheckOutMultipleEquipmentService`; `CheckoutTransaction` (one transaction, `FOR UPDATE`); `EquipmentViewModel.CheckOutAsync` | `T05-requests.txt`; `T05-*` readbacks (including the injected failure); `T07-03-*`, `T07-04-*`; `T08-*` (unknown outcome) |
| UC-07 View Student Borrowing History | BR-14, 18, 19 | API-02, API-03, API-15 | T01, T06, T07 | `StudentsController.List`, `Get`, `History`; `IBorrowingRepository.GetStudentHistoryAsync`; `StudentHistoryViewModel` | `T01-requests.txt`; `T06-*`; `T07-10-*` |
| UC-08 View Equipment Borrowing History | BR-14, 18, 19 | API-01, API-07, API-16 | T01, T06, T07 | `EquipmentController.List`, `Get`, `History`; `IBorrowingRepository.GetEquipmentHistoryAsync`; `EquipmentHistoryViewModel` | `T01-requests.txt`; `T06-*`; `T07-11-*` |
| UC-09 Search, Filter and Sort Borrowings | BR-17 to 19 | API-14 | T06, T07 | `BorrowingsController.List`; `BorrowingQueryService`; `EfBorrowingRepository.SearchAsync` (search, filter and sort run in PostgreSQL); `BorrowingSearchViewModel` | `T06-requests.txt`; `T06-*`; `T07-08-*` |
| UC-12 Register Student | BR-01, 02, 16, 18, 19 | API-04 | T02, T07 | `StudentsController.Create`; `RegisterStudentService`; unique index `UX_Students_StudentNumberKey`; `StudentsViewModel` | `T02R-requests.txt`; `T02R-*` readbacks; `T07-*` |
| UC-13 Update Student Information | BR-01 to 03, 14, 16, 18, 19 | API-02, API-03, API-05 | T02, T07 | `StudentsController.Update`; `UpdateStudentService`; `StudentsViewModel` | `T02R-requests.txt`; `T02R-*`; `T07-*` |
| UC-14 Change Student Borrowing Eligibility | BR-04, 08, 13, 14, 18, 19 | API-02, API-03, API-06 | T02, T07 | `StudentsController.SetEligibility`; `ChangeStudentEligibilityService`; `StudentsViewModel` | `T02R-requests.txt`; `T02R-04-*`, `T02R-05-*`; `T07-*` |
| UC-15 Register Equipment | BR-05, 06, 10, 16, 18, 19 | API-08 | T03, T07 | `EquipmentController.Create`; `RegisterEquipmentService`; unique index `UX_Equipment_IdentifierKey`; `EquipmentManagementViewModel` | `T03-requests.txt`; `T03-*`; `T07-12-*`, `T07-13-*` |
| UC-16 Update Equipment Information | BR-05 to 07, 10, 14, 16, 18, 19 | API-01, API-07, API-09 | T03, T07 | `EquipmentController.Update` (`isAvailable` is rejected as an unknown field); `UpdateEquipmentService`; `EquipmentManagementViewModel` | `T03-requests.txt`; `T03-*`; `T07-12-*` |

## 2. Cross-cutting requirements

| Requirement | Where it is met | Evidence |
| --- | --- | --- |
| Machine-readable OpenAPI description of API-01 to API-16 | `docs/openapi/openapi-v1.json`, generated from `/openapi/v1.json` | T01 |
| API runs without Avalonia | `src/EquipmentBorrowing.Api` started alone with `dotnet run` | T01 to T06 (requests sent with `scripts/Invoke-Lab5.ps1`) |
| No database code in the desktop | `EquipmentBorrowing.Desktop` references only `EquipmentBorrowing.Contracts`; no DbContext, SQL or Npgsql | `T07-14-*` and the `dotnet list ... reference` output |
| Atomic multi-item checkout, no partial writes | `CheckoutTransaction` and `EfBorrowingUnitOfWork` (one transaction) | T05 |
| Unknown outcome is reported, not retried (BR-18) | API returns 500 `outcome_unknown`; `ApiClient` maps it to `UnknownOutcome` | T08 (step 4) |
| No HTTP response vs received 500 | `ApiClient` (`Unreachable` vs `ServerError`); messages "No connection" and "Server error" | T08 (steps 1 and 2) |
| Persistence and restart | Restart checks after the changes | T02R, T03, T04R, T08 |
| Credentials out of the repository | user-secrets; `git grep -i "password="` shows only placeholders | final secrets check |

## 3. Continuity notice

| Item | Entry |
| --- | --- |
| Authentic Lab 4 submission | Tag `lab4-submitted`, commit `6e5dab2a2a8ffb00f00628a1de6214fc68550427` |
| Lab 4 baseline check | `dotnet build` succeeded and `dotnet test` passed 51 of 51 at the start of Lab 5 |
| Completed Lab 5 revision | Tag `lab5-submitted` (commit: TO COMPLETE at the end) |
| Lab 5 start date and deadline | 2026-11-19 and 2026-11-25 |

Refactoring and corrections disclosed (Lab 4 behaviour is unchanged):

- `CheckoutTransaction` and `ReturnEquipmentService` get an optional `IClock` parameter (fixed date for tests; the real date is used otherwise). The 51 tests still pass.
- `EfBorrowingRepository` now builds its borrowing query in one shared method and also implements `IBorrowingLookup` (read one borrowing by id).
- Corrected the success message of `ChangeStudentEligibilityService` (pasted PowerShell text was inside it). See F-01 in `Lab5_Evidence_Record.md`.
- Desktop view models were changed from Application services to HTTP clients, and the Application and Infrastructure references were removed from the desktop project.
- The API applies migrations at start but does not add the Lab 4 demo data. The controlled dataset is loaded with `--reset-test-data`.
- Visual Studio `.vs` folders were removed from Git tracking.