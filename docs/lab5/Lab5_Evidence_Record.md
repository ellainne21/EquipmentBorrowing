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
| Lab 5 revision and tag | TO COMPLETE at the end: commit hash and tag `lab5-submitted` |
| Before / after boundary | Before: the Desktop project called Application services and Infrastructure (EF Core / PostgreSQL) directly. After: TO COMPLETE |
| Migration / refactoring | TO COMPLETE (changed components, any new migration, how relationships stay intact) |
| Reproduction | TO COMPLETE (README Lab 5 section, API base URL, data reset, evidence locations) |

---

## 2. Test Execution Records

Environment for every test: TO COMPLETE (Windows, .NET 10 SDK, PostgreSQL 18, database name, API base URL, fixed clock date).

| Test | Status | Date run | Evidence reference |
|---|---|---|---|
| T01 API reads, relational lists, missing and empty | Not run | | |
| T02 Student create, edit, eligibility | Not run | | |
| T03 Equipment create, edit, protected state | Not run | | |
| T04 Single borrow and return | Not run | | |
| T05 Multi-item checkout, limit, rollback | Not run | | |
| T06 Histories, search, filters, sorting | Not run | | |
| T07 Avalonia HTTP client and refresh | Not run | | |
| T08 Unreachable API vs 500, recovery, restart | Not run | | |

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
| F-01 | 2026-11-19 | Code review at Lab 5 start | `ChangeStudentEligibilityService.cs` (submitted Lab 4 code) had pasted PowerShell text inside the success message shown when a student is restricted (the text "new borrowing" was cut and filled with `cd C:\\Users\\...` and `git` commands). Business behavior was correct; only the message text was wrong. Tests did not check this message. | Accidental paste into the wrong editor tab. | Message restored to: "{name} is now restricted from new borrowing. Existing borrowings stay active and can still be returned." Disclosed as a Lab 5 correction, not hidden. | TO COMPLETE (check the message through API-06 in T02). |

---

## 4. Controlled dataset and clock

Fixed local date used for tests: TO COMPLETE (2026-11-10 by the reference, or the actual date with the offsets recorded).
Dataset S1 to S4 and E1 to E7 is in Section 3 of the API and Acceptance Reference. Reset method: TO COMPLETE.

---

## 5. Final checklist (do before tagging)

- [ ] README Lab 5 section (setup, start API and desktop separately, migrations, reset, contract)
- [ ] OpenAPI file committed and compared with API-01 to API-16
- [ ] Traceability matrix completed (implementation reference and evidence for each use case)
- [ ] Baseline record above completed
- [ ] T01 to T08 results recorded honestly
- [ ] Secrets check: `git grep -i "password="` and `git log --all --oneline -S"Password="` show nothing real
- [ ] Tag `lab5-submitted` on the final commit, pushed to GitHub
