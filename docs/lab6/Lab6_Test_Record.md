# ITSD81 Laboratory Activity 6 — Traceability, Execution and Defect Record

Authors: *<full names and student IDs of both partners, or your own>*

All results below come from real runs of `dotnet test` against the disposable PostgreSQL test database with fixed date
**D = 2026-11-10**. Latest full run: **228 tests, 228 passed, 0 failed, 0 skipped** (55 pre-existing Lab 4/5 tests + 173 Lab 6 tests),
recorded in `docs/lab6/evidence/lab6-test-output.txt` and `lab6.trx`. No assertion is disabled or skipped.
Items marked *<fill in>* are not yet known and must be completed from real data, not guessed.

---

## 1. Implementation baseline record

| Field | Entry |
| --- | --- |
| Authentic Lab 5 submitted commit / tag | Commit `dd1e826f39ef2eed2069449b72e79ee746b9b4bd`, existing tag `lab5-submitted` (author Ella Sun, Sun Nov 22 2026 22:46:04 +0800, "Lab 5: final evidence record, traceability matrix and README Lab 5 section"). Also `origin/main` at that time. |
| How the baseline was verified; missing history or limitations | `git rev-parse lab5-submitted` printed the commit above; `git cat-file -t lab5-submitted` printed `commit`, so the tag is a **lightweight** tag (a plain pointer, no separate tagger or date). The tag existed before any Lab 6 work, and was not moved. The history had 110 commits at that time (`git log --oneline \| Measure-Object -Line`). No history is missing and none was recreated. `lab4-submitted` also exists. |
| Final Lab 6 commit and `lab6-submitted` tag | *<fill in after tagging: output of `git rev-parse lab6-submitted` and `git log -1 --format="%H %ad %s" lab6-submitted`>* |
| Test project(s), isolated PostgreSQL setup, fixture/reset and D | One test project, the existing `tests/EquipmentBorrowing.Tests` (xUnit 2.9.3, net10.0), Lab 6 code in its `Lab6/` folder; the pre-existing Lab 4/5 rule tests (fake-based) are kept untouched as part of the regression run. Database: a separate disposable PostgreSQL database whose name contains `test`, connection string from `dotnet user-secrets` key `ConnectionStrings:Test` (never in the repository); the test code refuses to run if the database name does not contain `test`. Reset: `ControlledDataset.Reset` (existing Lab 5 seam) before **every** test, which truncates with `RESTART IDENTITY` and loads S1–S4 / E1–E7. D = **2026-11-10** (`EQUIPMENT_BORROWING_FIXED_DATE`, same as the Lab 5 dataset). IDs are resolved from the API, not hard-coded. No SQLite or EF InMemory is used. |
| Significant refactoring and reproducible execution instructions | The only production source change is one line in `src/EquipmentBorrowing.Api/Program.cs`: `public partial class Program { }`, so `WebApplicationFactory<Program>` can start the real pipeline (no behavior change; verify with `git diff lab5-submitted --stat -- src`). Test-project changes: references to Api, Contracts, Infrastructure; packages `Microsoft.AspNetCore.Mvc.Testing` and `Npgsql`; the four desktop API-client source files are *linked* (not copied) into the test project. Steps to reproduce are in the README "Lab 6" section. |

Existing test seams (Lab 4/5, used as-is, active only with `EQUIPMENT_BORROWING_DB=Test`): fixed clock, `--reset-test-data`, `AfterFirstWrite`, `LostCommit`, `BrokenConnection`.

---

## 2. Requirements traceability matrix (UC → BR → API → TC6 → actual test → result → evidence)

Test classes are in `tests/EquipmentBorrowing.Tests/Lab6/`; method names are listed in `docs/lab6/evidence/lab6-test-list.txt` (`dotnet test --list-tests`).
All results are **Passed** in the 228/228 run dated *<run date from the output>*.

| UC | BR | API | TC6 | Actual test implementation | Execution result | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| UC-01 | BR-05–07,10,19 | 01 | U01, E07 | `TC6_U01_CatalogTests` (ReadOnlyUseCaseTests.cs); E07: `TC6_E07_TransportAndUncertainOutcomeTests`, `TC6_E07_ClientDoubleTests`, `TC6_E07_ClientAgainstRealApiTests` | Passed | test output + trx; E07 read path (GET equipment) in `TC6_E07_*` |
| UC-02 | BR-10,19 | 01 | U02 | `TC6_U02_AvailableFilteringTests` (includes one labeled characterization test, see L6-01) | Passed | test output + trx |
| UC-03 | BR-08–11,14–16,19 | 10, 13 | U03, E02, E03, E05, E07, E08 | `TC6_U03_SingleBorrowingTests` (WriteUseCaseTests.cs); `TC6_E02_LimitBoundaryTests`; `TC6_E03_ProtocolErrorTests`; `TC6_E05_StaleAndConflictingStateTests`; `TC6_E07_*`; `TC6_E08_FreshConnectionsAndRestartTests` | Passed | test output + trx; fresh-connection readbacks are asserted inside the tests |
| UC-04 | BR-14,19 | 14 | U04, U09 | `TC6_U04_ActiveBorrowingsTests`; `TC6_U09_SearchAndOrderingTests` | Passed | test output + trx |
| UC-05 | BR-10,13–14,19 | 12, 13 | U05, U14, E08 | `TC6_U05_ReturnAndHistoryTests`; `TC6_U14_EligibilityTransitionsTests`; `TC6_E08_FreshConnectionsAndRestartTests` | Passed | test output + trx |
| UC-06 | BR-08–12,14–16,19 | 11 | U06, E02–E05, E07 | `TC6_U06_MultiItemCheckoutTests`; `TC6_E02_*`; `TC6_E03_*`; `TC6_E04_PostgresRollbackTests`; `TC6_E05_*`; `TC6_E07_*` | Passed | test output + trx; rollback readback in E04 |
| UC-07 | BR-14,19 | 03, 15 | U07 | `TC6_U07_StudentHistoryTests` | Passed | test output + trx |
| UC-08 | BR-14,19 | 07, 16 | U08, U16 | `TC6_U08_EquipmentHistoryTests`; `TC6_U16_UpdateEquipmentTests` | Passed | test output + trx |
| UC-09 | BR-17,19 | 14 | U09, E03 | `TC6_U09_SearchAndOrderingTests`; `TC6_E03_ProtocolErrorTests` | Passed | test output + trx |
| UC-12 | BR-01–02,16,19 | 03, 04 | U12, E01, E08 | `TC6_U12_RegisterStudentTests`; `TC6_E01_NormalizedUniquenessTests`, `TC6_E01_DatabaseIntegrityTests`; `TC6_E08_*` | Passed | test output + trx |
| UC-13 | BR-01–03,14,16,19 | 02, 03, 05, 15 | U13, E01, E06 | `TC6_U13_UpdateStudentTests`; `TC6_E01_*`; `TC6_E06_OptionalAndProtectedFieldTests` | Passed | test output + trx |
| UC-14 | BR-04,08,13–14,19 | 06, 10, 12 | U14 | `TC6_U14_EligibilityTransitionsTests` | Passed | test output + trx |
| UC-15 | BR-05–06,10,16,19 | 07, 08 | U15, E01, E08 | `TC6_U15_RegisterEquipmentTests`; `TC6_E01_*`; `TC6_E08_*` | Passed | test output + trx |
| UC-16 | BR-05–07,10,14,16,19 | 07, 09, 16 | U16, E01, E06 | `TC6_U16_UpdateEquipmentTests`; `TC6_E01_*`; `TC6_E06_*` | Passed | test output + trx |

### API coverage check (each API exercised through the real HTTP pipeline)

| API | Route | Automated HTTP test(s) | Result |
| --- | --- | --- | --- |
| 01 | GET /api/v1/equipment | `TC6_U01_*`, `TC6_U02_*`, `TC6_OpenApi_ConsistencyTests` | Passed |
| 02 | GET /api/v1/students | `TC6_U13_UpdateStudentTests` (search hit and miss), OpenAPI success-shape test | Passed |
| 03 | GET /api/v1/students/{id} | `TC6_U07_*`, `TC6_U12_*`, `TC6_U13_*` | Passed |
| 04 | POST /api/v1/students | `TC6_U12_*`, `TC6_E01_NormalizedUniquenessTests`, `TC6_E03_*` | Passed |
| 05 | PUT /api/v1/students/{id} | `TC6_U13_*`, `TC6_E01_*`, `TC6_E06_*` | Passed |
| 06 | PATCH /api/v1/students/{id}/eligibility | `TC6_U14_*`, `TC6_OpenApi_*` | Passed |
| 07 | GET /api/v1/equipment/{id} | `TC6_U08_*`, `TC6_U15_*`, `TC6_U16_*` | Passed |
| 08 | POST /api/v1/equipment | `TC6_U15_*`, `TC6_E01_*`, `TC6_E03_*` | Passed |
| 09 | PUT /api/v1/equipment/{id} | `TC6_U16_*`, `TC6_E01_*`, `TC6_E06_*` | Passed |
| 10 | POST /api/v1/borrowings | `TC6_U03_*`, `TC6_U14_*`, `TC6_E02_*`, `TC6_E03_*`, `TC6_E05_*`, `TC6_E08_*` | Passed |
| 11 | POST /api/v1/checkout-requests | `TC6_U06_*`, `TC6_E02_*`, `TC6_E03_*`, `TC6_E04_*`, `TC6_E05_*`, `TC6_E07_*` | Passed |
| 12 | POST /api/v1/borrowings/{id}/return | `TC6_U05_*`, `TC6_U14_*`, `TC6_E08_*` | Passed |
| 13 | GET /api/v1/borrowings/{id} | `TC6_U03_*`, `TC6_U05_*`, `TC6_E08_*` | Passed |
| 14 | GET /api/v1/borrowings | `TC6_U04_*`, `TC6_U09_*`, `TC6_E03_*` | Passed |
| 15 | GET /api/v1/students/{id}/borrowings | `TC6_U07_*`, `TC6_U13_*` | Passed |
| 16 | GET /api/v1/equipment/{id}/borrowings | `TC6_U08_*`, `TC6_U16_*` | Passed |

OpenAPI consistency (`TC6_OpenApi_ConsistencyTests`, 6 tests): 16 operations exactly; query/path parameters; documented schemas equal the C# contract types; protected fields absent from update requests; each API returns its documented success status with a body matching the documented schema; ten real error statuses are documented for their operation with the documented error shape. **Passed.**

---

## 3. Test execution record

Status values: Passed / Failed / Blocked / Not Run. "Passed" means the test ran with its assertions enabled in the 228/228 run.

| Test Case ID | Actual test implementation / manual method | Expected result | Observed result | Status | Defect ref | Correction | Retest | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TC6-U01 | `TC6_U01_CatalogTests` | Seven items with IDs, names, availability; empty catalog gives `[]`; database unchanged | As expected | Passed | — | — | — | test output, trx |
| TC6-U02 | `TC6_U02_AvailableFilteringTests` | `available=true` returns only E2/E3/E6/E7; none available gives `[]` | As expected | Passed | L6-01 | Invented extra assertion replaced by a labeled characterization test | 228/228 | test output, trx |
| TC6-U03 | `TC6_U03_SingleBorrowingTests` | 201 + Location, one active record dated D, item unavailable, no unrelated change | As expected | Passed | — | — | — | test output, trx |
| TC6-U04 | `TC6_U04_ActiveBorrowingsTests` | Exactly E1/E4/E5 with correct students; returned E3 excluded | As expected | Passed | — | — | — | test output, trx |
| TC6-U05 | `TC6_U05_ReturnAndHistoryTests` | Returned dated D, same record kept, item available, repeat is 409 with no change | As expected | Passed | — | — | — | test output, trx |
| TC6-U06 | `TC6_U06_MultiItemCheckoutTests` | Two active records, both items unavailable, others unchanged | As expected | Passed | — | — | — | test output, trx |
| TC6-U07 | `TC6_U07_StudentHistoryTests` | S1 active + returned history with dates and status; S4 `[]`; missing student 404 | As expected | Passed | — | — | — | test output, trx |
| TC6-U08 | `TC6_U08_EquipmentHistoryTests` | Correct student/item relations; E2 `[]`; missing item 404 | As expected | Passed | — | — | — | test output, trx |
| TC6-U09 | `TC6_U09_SearchAndOrderingTests` | Case-insensitive partial search per field, status filters, both sorts both directions, ties, combined criteria | As expected | Passed | — | — | — | test output, trx |
| TC6-U12 | `TC6_U12_RegisterStudentTests` | 201, generated ID, Location, eligible by default, durable row | As expected | Passed | — | — | — | test output, trx |
| TC6-U13 | `TC6_U13_UpdateStudentTests` | Search; edit changes name/contact only; history and foreign keys intact | As expected | Passed | — | — | — | test output, trx |
| TC6-U14 | `TC6_U14_EligibilityTransitionsTests` | Restrict blocks a new loan (409), not the return; permit restores issuing | As expected | Passed | — | — | — | test output, trx |
| TC6-U15 | `TC6_U15_RegisterEquipmentTests` | 201, available by default; duplicate code 409, one row | As expected | Passed | — | — | — | test output, trx |
| TC6-U16 | `TC6_U16_UpdateEquipmentTests` | Description edit keeps identity, unavailable state, active history | As expected | Passed | — | — | — | test output, trx |
| TC6-E01 | `TC6_E01_NormalizedUniquenessTests`, `TC6_E01_DatabaseIntegrityTests` | Case/whitespace variants rejected (409) at API; PostgreSQL rejects direct duplicates (23505), CHECK (23514), FK (23503) and RESTRICT delete (23001) | As expected | Passed | L6-02 | Expected SQLSTATE corrected from 23503 to 23001 for `ON DELETE RESTRICT` | 146/146, then 228/228 | test output, trx |
| TC6-E02 | `TC6_E02_LimitBoundaryTests` | Third item accepted (single and multi), over-limit 409 with no partial write | As expected | Passed | — | — | — | test output, trx |
| TC6-E03 | `TC6_E03_ProtocolErrorTests` (about 40 cases) | Documented 400/404/405/409, safe `{code,message}` body, database unchanged | As expected | Passed | — | — | — | test output, trx |
| TC6-E04 | `TC6_E04_PostgresRollbackTests` | Fault after write 1 of 2: safe 500, fresh readback equals baseline, with host alive and after disposal | As expected | Passed | — | — | — | test output, trx |
| TC6-E05 | `TC6_E05_StaleAndConflictingStateTests` | Stale single and multi requests 409; direct second active row refused (23505); simultaneous requests give one winner | As expected (race observed over 5 rounds) | Passed | — | — | — | test output, trx |
| TC6-E06 | `TC6_E06_OptionalAndProtectedFieldTests` | NULL clearing; blank required 400; duplicates 409; `isEligible` / `isAvailable` in a PUT rejected | As expected | Passed | — | — | — | test output, trx |
| TC6-E07 | Server: `TC6_E07_TransportAndUncertainOutcomeTests`; client: `TC6_E07_ClientDoubleTests`, `TC6_E07_ClientAgainstRealApiTests` | No response = Unreachable; reachable 500 = server error; lost commit = unknown outcome, no retry, committed state verified in PostgreSQL | As expected | Passed | L6-03 | Expected message text replaced by comparison with the server's actual message | 228/228 | test output, trx |
| TC6-E08 | Part 1 automated fresh connection and context: `TC6_E08_FreshConnectionsAndRestartTests`; Part 2 **documented integration harness** `ApiProcess` (real OS process stop/start, two PIDs logged) | State survives disposed hosts and a real API process restart; equal to fresh PostgreSQL | As expected | Passed | — | — | — | test output, trx; the two PIDs and ports are in `docs/lab6/evidence/E08-restart-pids.txt` (copied unchanged from the test's StdOut in `lab6.trx`; the console file does not hold per-test output) |
| MA6-01 | Manual observation of the desktop (no UI automation) | Responsive UI, loading state, understandable unreachable handling, no unintended writes | *<fill in>* | **Not Run** *(update after the manual check)* | — | — | — | *<screenshots or recording, named MA6-01_*>* |
| MA6-02 | Automated: `MA6_02_IndependentApiAndSynchronizationTests`, `TC6_OpenApi_ConsistencyTests`, `TC6_Architecture_DesktopBoundaryTests`. Manual: change data from an independent client, refresh the desktop | Independent API works, OpenAPI consistent, desktop is HTTP-only, UI refreshes | Automated parts as expected; manual UI refresh *<fill in>* | Automated parts **Passed**; manual part **Not Run** *(update)* | — | — | — | test output, trx; *<MA6-02_* evidence>* |

---

## 4. Defect and regression evidence

No application defect was found by these tests. Four **test-code** problems were found, investigated, corrected and retested; they are kept here
rather than hidden. Environment for all: Windows, .NET SDK 10.0.401, net10.0, disposable PostgreSQL test database, D = 2026-11-10.

| ID | Affected | Expected vs observed | Likely layer and diagnosis | Correction | Retest and regression |
| --- | --- | --- | --- | --- | --- |
| L6-01 *(date: fill in)* | TC6-U02; UC-02; BR-10; API-01 | Test expected `available=false` to return only borrowed items; observed all seven | Test design. `EquipmentController`/`EquipmentQueryService`/`EfEquipmentRepository`: API-01 is "all items, or only available ones with `available=true`"; `false` is not a filter. The Lab 6 suite requires only `available=true`. | Replaced with a labeled characterization test (`Available_false_is_not_a_filter...`); required U02 assertions unchanged | `dotnet test`: 104/104, later 228/228 |
| L6-02 *(date: fill in)* | TC6-E01 (database integrity); UC-12/15; BR-14, BR-16 | Expected SQLSTATE 23503 on deleting a student/item with history; observed 23001, delete refused, database unchanged | Test design. `AppDbContext` uses `DeleteBehavior.Restrict` (`ON DELETE RESTRICT`), which PostgreSQL reports as 23001 (23503 is for `NO ACTION`) | Assert 23001; the snapshot-unchanged assertion kept | 146/146, later 228/228 |
| L6-03 *(date: fill in)* | TC6-E07 client side; UC-06; BR-18; API-11 | Expected client message to contain "no changes were made" (written from memory); observed a different, generic-sounding message | Test code first. Lab 5 evidence shows the server's broken-connection messages differ. Revised test compares the client's message with the server's own message for the same request: **identical**, so the client handles it correctly. | Compare with the server's actual message instead of a hard-coded phrase | The E07b client test passes; 228/228 |
| L6-04 *(date: fill in)* | Infrastructure smoke test (not a TC6 case) | Date columns read as dates; observed `InvalidCastException`, `DateOnly` to `DateTime` | Test code. Npgsql maps PostgreSQL `date` to `DateOnly` | Compare as `DateOnly` | 60/60 at that stage, later 228/228 |

Evidence locations: `docs/lab6/evidence/lab6-test-output.txt` (console output of the full run), `lab6.trx`, `lab6-test-list.txt`, `E08-restart-pids.txt` (E08 process IDs, copied unchanged from `lab6.trx`); per-failure console output is kept in the
terminal logs *<attach if retained>*. Fresh PostgreSQL readbacks are performed inside the tests themselves (non-pooled connections), so the assertions are the readback evidence.

---

## 5. Limits, assumptions and specification observations (disclosure)

What each kind of check can and cannot prove:

* **HTTP test doubles** (`StubHandler`, `CountingHandler`): can simulate no response, a slow server and canned statuses to test the desktop client's handling. They cannot prove persistence, rollback or server behavior; those claims use the real API and genuine PostgreSQL.
* **Server fault seams** (`AfterFirstWrite`, `LostCommit`, `BrokenConnection`): inject a failure inside a real PostgreSQL transaction or at the commit acknowledgement. They prove rollback and unknown-outcome handling for those failures; they are not a real network drop or a process crash.
* **Desktop client source is linked, not the Desktop assembly:** the four `Desktop/Api/*.cs` files compile into the test project, so the real client code is tested; the Avalonia UI and view models are not. UI behavior is covered by MA6-01/MA6-02 manual evidence.
* **Concurrency (E05):** the simultaneous-request test was observed over 5 fresh rounds with consistent results; a timing test cannot prove a race can never occur. The database's partial unique index is also tested directly.
* **E01:** PostgreSQL cannot normalize text by itself; the API tests prove normalization, the direct-insert tests prove the unique index on the stored normalized key exists.
* **Restart (E08):** verified by an automated integration harness that starts and kills the real API as an operating-system process (PIDs differ: 21280 and 1192, see `docs/lab6/evidence/E08-restart-pids.txt`); in-process host re-creation is additionally used for the fresh-context checks.

Specification observations (confirm against the Lab 4 text; document, do not silently change): *<fill in after checking>*
1. UC-09 ordering: ties on equal dates are ordered by borrowing ID **in the same direction as the sort** (observed in Lab 5 and asserted in U09).
2. UC-09 search covers student name, student number, equipment code and equipment name.
3. API-01 `available=false` is not a filter (L6-01).
4. 405 (wrong method) and unknown-route 404 are returned with the standard error shape but are not listed per operation in the OpenAPI description.

Other disclosures: the test seams live in the production `Infrastructure/Testing` folder and are active only in Test database mode; `Status = 0` is Active in the database; numeric/enum assumptions are asserted only as "not Active" where the value is not guaranteed.
