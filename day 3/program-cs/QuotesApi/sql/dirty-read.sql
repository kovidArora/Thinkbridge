CREATE TABLE IsolationDemo (
    Id INT NOT NULL PRIMARY KEY,
    Status VARCHAR(20) NOT NULL,
    Amount DECIMAL(10,2) NOT NULL
);

INSERT INTO IsolationDemo (Id, Status, Amount) VALUES
    (1, 'Pending', 100.00),
    (2, 'Pending', 200.00),
    (3, 'Pending', 300.00);

-- ============================================================

-- SESSION A
BEGIN TRAN;
UPDATE IsolationDemo SET Amount = 999.00 WHERE Id = 1;

-- SESSION B
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SELECT Id, Amount FROM IsolationDemo WHERE Id = 1;

-- SESSION A
ROLLBACK;

SELECT Id, Amount FROM IsolationDemo WHERE Id = 1;

-- SESSION A
BEGIN TRAN;
UPDATE IsolationDemo SET Amount = 999.00 WHERE Id = 1;

-- SESSION B (blocks until Session A finishes)
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SELECT Id, Amount FROM IsolationDemo WHERE Id = 1;

-- SESSION A
ROLLBACK;

DROP TABLE IsolationDemo;
