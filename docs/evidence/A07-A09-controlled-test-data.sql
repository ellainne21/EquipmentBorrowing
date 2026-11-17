-- Lab 4, A07 to A09: controlled test data for history, search, filter and sort.
-- RUN ONLY ON THE DISPOSABLE TEST DATABASE: equipmentborrowing_test
--
-- It adds 4 RETURNED borrowings (Status = 1), so:
--   * no equipment availability changes
--   * the "one active borrowing per item" rule is not touched
--   * Checkout-date order and Expected-return-date order become DIFFERENT
--   * there are ties on both date fields, so the borrowing id tie-breaker (BR-17) is tested
--
-- Run the whole script once. It stops by itself if it is run on the wrong
-- database or if it was already run.

DO $$
DECLARE
    daniel   integer;
    ella     integer;
    mic      integer;
    cam      integer;
    lap      integer;
    prj      integer;
BEGIN
    IF current_database() <> 'equipmentborrowing_test' THEN
        RAISE EXCEPTION 'Run this script on equipmentborrowing_test only. Current database: %', current_database();
    END IF;

    IF EXISTS (SELECT 1 FROM "Borrowings" WHERE "ExpectedReturnDate" = DATE '2026-12-20') THEN
        RAISE EXCEPTION 'The controlled data was already added. Nothing was changed.';
    END IF;

    SELECT "Id" INTO STRICT daniel FROM "Students"  WHERE "StudentNumberKey" = '2026-0004';
    SELECT "Id" INTO STRICT ella   FROM "Students"  WHERE "StudentNumberKey" = 'ENG-2026-01';
    SELECT "Id" INTO STRICT mic    FROM "Equipment" WHERE "IdentifierKey"    = 'MIC-001';
    SELECT "Id" INTO STRICT cam    FROM "Equipment" WHERE "IdentifierKey"    = 'CAM-001';
    SELECT "Id" INTO STRICT lap    FROM "Equipment" WHERE "IdentifierKey"    = 'LAP-001';
    SELECT "Id" INTO STRICT prj    FROM "Equipment" WHERE "IdentifierKey"    = 'PRJ-001';

    -- Inserted one by one, so their ids rise in this order (C1 < C2 < C3 < C4).

    -- C1: long loan. Early checkout, LATEST due date.
    INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "ReturnedDate", "Status")
    VALUES (daniel, mic, DATE '2026-11-04', DATE '2026-12-20', DATE '2026-11-10', 1);

    -- C2: same checkout date as C3 (tie on checkout date), due date 11-12.
    INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "ReturnedDate", "Status")
    VALUES (ella, cam, DATE '2026-11-10', DATE '2026-11-12', DATE '2026-11-11', 1);

    -- C3: same checkout date as C2 (tie on checkout date), due date 11-17.
    INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "ReturnedDate", "Status")
    VALUES (ella, lap, DATE '2026-11-10', DATE '2026-11-17', DATE '2026-11-12', 1);

    -- C4: same due date as C2 (tie on due date), earlier checkout.
    INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "ReturnedDate", "Status")
    VALUES (daniel, prj, DATE '2026-11-05', DATE '2026-11-12', DATE '2026-11-08', 1);
END $$;

-- Expected-order queries. Run these ONE AT A TIME and compare with the Borrowing Search page.

-- 1) Checkout date, ascending (ties: lower id first)
-- SELECT b."Id", s."StudentNumber", e."Identifier", b."DateBorrowed", b."ExpectedReturnDate", b."ReturnedDate", b."Status"
-- FROM "Borrowings" b JOIN "Students" s ON s."Id" = b."StudentId" JOIN "Equipment" e ON e."Id" = b."EquipmentId"
-- ORDER BY b."DateBorrowed" ASC, b."Id" ASC;

-- 2) Expected return date, ascending (ties: lower id first)
-- SELECT b."Id", s."StudentNumber", e."Identifier", b."DateBorrowed", b."ExpectedReturnDate", b."ReturnedDate", b."Status"
-- FROM "Borrowings" b JOIN "Students" s ON s."Id" = b."StudentId" JOIN "Equipment" e ON e."Id" = b."EquipmentId"
-- ORDER BY b."ExpectedReturnDate" ASC, b."Id" ASC;
