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
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRAN;
SELECT Id, Amount FROM IsolationDemo WHERE Id = 2;

-- SESSION B
UPDATE IsolationDemo SET Amount = 250.00 WHERE Id = 2;

-- SESSION A
SELECT Id, Amount FROM IsolationDemo WHERE Id = 2;
COMMIT;

UPDATE IsolationDemo SET Amount = 200.00 WHERE Id = 2;

-- SESSION A
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
SELECT Id, Amount FROM IsolationDemo WHERE Id = 2;

-- SESSION B (blocks until Session A commits)
UPDATE IsolationDemo SET Amount = 250.00 WHERE Id = 2;

-- SESSION A
SELECT Id, Amount FROM IsolationDemo WHERE Id = 2;
COMMIT;

DROP TABLE IsolationDemo;
