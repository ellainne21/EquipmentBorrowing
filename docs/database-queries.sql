-- ============================================================
-- Campus Equipment Borrowing System
-- database-queries.sql
-- Demonstrates SQL understanding against the relational schema
-- ============================================================

-- ------------------------------------------------------------
-- 1. Basic Retrieval
-- Retrieve all equipment.
-- ------------------------------------------------------------
SELECT *
FROM Equipment;


-- ------------------------------------------------------------
-- 2. Filtering
-- Retrieve only currently available equipment.
-- ------------------------------------------------------------
SELECT *
FROM Equipment
WHERE IsAvailable = 1;


-- ------------------------------------------------------------
-- 3. Join
-- Retrieve active borrowings together with the corresponding
-- student and equipment information.
-- ------------------------------------------------------------
SELECT
    s.Name        AS Student,
    e.Name        AS Equipment,
    b.DateBorrowed        AS Borrowed,
    b.ExpectedReturnDate  AS Due
FROM Borrowings b
INNER JOIN Students s  ON b.StudentId  = s.Id
INNER JOIN Equipment e ON b.EquipmentId = e.Id
WHERE b.Status = 0; -- 0 = Active


-- ------------------------------------------------------------
-- 4. Aggregate
-- Number of active borrowings per student.
-- ------------------------------------------------------------
SELECT
    s.Name AS Student,
    COUNT(b.Id) AS ActiveBorrowings
FROM Students s
LEFT JOIN Borrowings b
    ON b.StudentId = s.Id
    AND b.Status = 0 -- 0 = Active
GROUP BY s.Id, s.Name;


-- ------------------------------------------------------------
-- 5. Update
-- Mark a specific equipment item as unavailable
-- (example: EquipmentId = 1).
-- ------------------------------------------------------------
UPDATE Equipment
SET IsAvailable = 0
WHERE Id = 1;