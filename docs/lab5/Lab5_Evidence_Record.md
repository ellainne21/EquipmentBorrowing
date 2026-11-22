# Lab 5 Evidence Record (API Integration and Acceptance Testing)

**Pair:** Ellainne G. Cabasag (2023304057) and Floramay S. Balofinos (2024302506)
**Repository:** https://github.com/ellainne21/EquipmentBorrowing

This is the one consolidated record for Lab 5. Nothing is marked Pass until it was run and checked
against a fresh PostgreSQL readback. Tests not run are marked **Not run** with the reason.

---

## 1. Implementation Baseline Record

| Field | Entry |
|---|---|
| Lab 4 submitted commit | `6e5dab2a2a8ffb00f00628a1de6214fc68550427`, tag `lab4-submitted`. Identified with `git rev-parse lab4-submitted`. On the day of checking it was the same commit as `HEAD`, `main` and `origin/main`. |
| Baseline checks (Lab 5 start) | `dotnet build`: succeeded (5 projects). `dotnet test`: 51 total, 51 passed, 0 failed. |
| Baseline limitations | At Lab 5 start `git status` briefly listed `EfBorrowingRepository.cs` as modified; `git diff` showed no content difference and `git restore` made the tree clean (see F-00). The code review at the start of Lab 5 found a defect in the Lab 4 submission (see F-01). |
| Lab 5 start date | 2026-11-19 |
| Lab 5 deadline | 2026-11-25 (as given by the instructor; time to be confirmed in Google Classroom) |
| Lab 5 revision and tag | Tag `lab5-submitted` (the commit it points to is shown by `git rev-parse lab5-submitted`). Baseline: tag `lab4-submitted`, commit 6e5dab2a2a8ffb00f00628a1de6214fc68550427. `lab5-submitted` |
| Before / after boundary | Before: the Desktop project called Application services and Infrastructure (EF Core / PostgreSQL) directly. After: the Desktop project references only `EquipmentBorrowing.Contracts` and calls the API over HTTP through `ApiClient` (commit `d961401` removed the database code and the Infrastructure and Application references). The API owns the DbContext, repositories and migrations. |
| Migration / refactoring | No new migration was added. `git diff --name-status lab4-submitted HEAD -- src/EquipmentBorrowing.Infrastructure` lists no file under `Migrations`, so the Lab 4 migrations are reused unchanged and the schema and relationships (students, equipment, borrowings and their foreign keys) stay as in Lab 4. Infrastructure changes since `lab4-submitted`: `Repositories/EfBorrowingRepository.cs` modified; `Testing/ControlledDataset.cs` and `Testing/FixedClock.cs` added (controlled test dataset and fixed clock, active only with the test database). New projects: `EquipmentBorrowing.Api` and `EquipmentBorrowing.Contracts`. Changed projects: Api, Application, Contracts, Desktop, Domain, Infrastructure (50 files in `src`, 1753 insertions, 452 deletions). The desktop was refactored from direct service and database calls to `ApiClient` over HTTP, and the business rules stay on the server. Details: `git diff lab4-submitted HEAD`. |
| Reproduction | README section "Laboratory Activity 5": secrets on the API project, starting the API and the desktop in two windows, API base URL `http://localhost:5080`, data reset, endpoint contract and `docs/openapi/openapi-v1.json`. Evidence is in `docs/lab5/evidence/`. |

---

## 2. Test Execution Records

Environment for every test: Windows, .NET 10 SDK, PostgreSQL 18, disposable database `equipmentborrowing_test` (never the real database), API base URL `http://localhost:5080`, API started with `EQUIPMENT_BORROWING_DB=Test` and `EQUIPMENT_BORROWING_FIXED_DATE=2026-11-10`, fault mode none unless a test says otherwise. Requests are sent and logged by `scripts\Invoke-Lab5.ps1`.

| Test | Status | Date run | Evidence reference |
|---|---|---|---|
| T01 API reads, relational lists, missing and empty | Pass | 2026-11-20 | `T01-requests.txt` (every read 200; unknown IDs and bad route 404; `DELETE` 405; empty history `[]`), `T01-02-counts-after.JPG`, `T01-openapi-check.txt` (16 operations compared with API-01 to API-16, 0 extra) |
| T02 Student create, edit, eligibility | Pass (retested) | 2026-11-20, 2026-11-21, 2026-11-22 | First run `T02-*` lacked some database readbacks. Rerun T02R (2026-11-21): log in `T02-requests.txt` after the RERUN marker, screenshots `T02R-01` to `T02R-06`. Rerun T02R2 (2026-11-22): `T02R2-requests.txt` and `T02R2-03` to `T02R2-05`. Both reruns: 201, 200, 200, 400, 409, restrict 200, new issue 409, return 200, permit 200, issue 201, repeat 200, restart reads 200 |
| T03 Equipment create, edit, protected state | Pass | 2026-11-20 | `T03-requests.txt` (20:31 to 20:34): create 201 with Location and `isAvailable` true; rename 200; description change 200 with E1 still unavailable; blank name 400; duplicate `eq-001` 409; `isAvailable` in the body 400 (protected field); E1 history 200 with its one active borrowing; reads after the restart 200 and unchanged. Readbacks `T03-02-equipment-after` (8 rows: E1 unavailable with the new description, E8 available) and `T03-02-borrowings-unchanged` (4 rows, borrowing 1 still active) |
| T04 Single borrow and return | Pass (rerun as T04R) | 2026-11-20, 2026-11-22 | `T04R-requests.txt` 17:37 to 17:44: checkout 201; six rejections 409, 409, 404, 404, 400, 400; return 200; repeat return 409; restart reads 200 and 200. Screenshots `T04R-01` to `T04R-04`. The requests at 21:18 are an accidental re-run, annotated in the log. First run files `T04-*` are kept |
| T05 Multi-item checkout, limit, rollback | Pass | 2026-11-20 to 2026-11-21 | `T05-requests.txt`; steps 1, 2a, 2b, 2c, 3 and 4 each with before, responses and after screenshots; `T05-04-api-log.JPG` (injected failure after 1 of 2 writes, database unchanged) |
| T06 Histories, search, filters, sorting | Pass | 2026-11-21 | `T06-requests.txt`; `T06-01-before`, `T06-01-histories`, `T06-02a-search-active`, `T06-02b-returned-sort`, `T06-03-combined-invalid`, `T06-04-invalid-sort`, `T06-03-after` (read-only, database unchanged). The first requests at 08:16 and 08:17 hit a stopped API and are annotated |
| T07 Avalonia HTTP client and refresh | Pass | 2026-11-21 | `T07-requests.txt`; app screenshots `T07-02-students-api-stopped` and `T07-02-students-recovered` (the Students page shows an error while the API is stopped and loads again after it is started); `T07-03-checkout-success` (two items checked out together), `T07-04-checkout-rejected` (restricted student, "Not allowed", nothing changed), `T07-05-db-after-checkout` (database readback), `T07-06-active-borrowings`, `T07-07-return` (list refreshed from the API), `T07-08-search-returned`, `T07-10-student-history`, `T07-11-equipment-history`, `T07-12-manage-equipment`, `T07-13-db-after-manage-equipment` (EQ-009 saved, duplicate EQ-001 rejected). No database code in the desktop: `dotnet list src/EquipmentBorrowing.Desktop reference` shows only `EquipmentBorrowing.Contracts`, and a code search for DbContext, Npgsql, EntityFramework, Infrastructure and Application types finds nothing |
| T08 Unreachable API vs 500, recovery, restart | Pass | 2026-11-21 | `T08-requests.txt`; `T08-01` to `T08-11`: stopped API gives no HTTP response (curl exit 7, app "No connection"); broken connection gives a safe 500 (app "Server error", no ST-900 saved); recovery 200 with counts 4, 7, 4; lost commit gives 500 `outcome_unknown` (app "Result unknown", no retry, borrowings 5 and 6 only) |

### Record template (copy once per test and per retest)

| Field | Entry |
|---|---|
| Test ID / date / environment | T__ / date / environment |
| Expected result | |
| Observed result | |
| Pass / fail | |
| Issue / diagnosis | |
| Correction | |
| Retest outcome | |
| Evidence reference | |

---

## 3. Failure, Diagnosis, Correction and Retest Log

| ID | Date | Where found | What happened | Diagnosis | Fix (commit) | Retest result |
|---|---|---|---|---|---|---|
| F-00 | 2026-11-19 | Baseline check | `git status` listed `src/EquipmentBorrowing.Infrastructure/Repositories/EfBorrowingRepository.cs` as modified after tag `lab4-submitted`. | `git diff` printed no content difference. The open editor tab also showed two pasted PowerShell comment lines at the end of the file that were not saved to disk. Cause of the stale status not proven. | `git restore` on that file; `git status` then showed a clean tree. No code change. | `git status`: working tree clean. |
| F-01 | 2026-11-19 | Code review at Lab 5 start | `ChangeStudentEligibilityService.cs` (submitted Lab 4 code) had pasted PowerShell text inside the success message shown when a student is restricted (the text "new borrowing" was cut and filled with `cd C:\\Users\\...` and `git` commands). Business behavior was correct; only the message text was wrong. Tests did not check this message. | Accidental paste into the wrong editor tab. | Message restored to: "{name} is now restricted from new borrowing. Existing borrowings stay active and can still be returned." Disclosed as a Lab 5 correction, not hidden. | 2026-11-22: the API does not return this message (PATCH `/students/{id}/eligibility` answers with the student record), so the retest is a unit test, `tests/EquipmentBorrowing.Tests/StudentEligibilityTests.cs`. It asserts the exact sentence, that it contains no `cd C:`, `git` or `PowerShell` text, that existing borrowings stay active, and the permit and already-restricted messages. `dotnet test`: 55 passed, 0 failed. A code search finds no pasted shell text in any `.cs` file. |

---

## 4. Controlled dataset and clock

Fixed local date used for tests: 2026-11-10, set with `EQUIPMENT_BORROWING_FIXED_DATE` (honoured only with the test database). The timestamps in the request logs are the real clock of the computer (2026-11-20 and 2026-11-21). The API uses 2026-11-10 for borrowed and returned dates, so no offsets are needed.
Dataset S1 to S4 and E1 to E7 is in Section 3 of the API and Acceptance Reference. Reset method: with the test variables set, run `dotnet run --project src/EquipmentBorrowing.Api -- --reset-test-data`. It reloads S1 to S4, E1 to E7 and borrowings 1 to 4 in the test database only, then exits. The API is then started again without the flag.

---

## 5. Final checklist (do before tagging)

- [ ] README Lab 5 section (setup, start API and desktop separately, migrations, reset, contract)
- [ ] OpenAPI file committed and compared with API-01 to API-16
- [ ] Traceability matrix completed (implementation reference and evidence for each use case)
- [ ] Baseline record above completed
- [ ] T01 to T08 results recorded honestly
- [x] Secrets check: `git grep -i "password="` and `git log --all --oneline -S"Password="` show nothing real
- [ ] Tag `lab5-submitted` on the final commit, pushed to GitHub
