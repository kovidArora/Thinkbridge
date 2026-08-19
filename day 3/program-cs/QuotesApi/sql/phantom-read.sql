CREATE TABLE IsolationDemo (
    Id INT NOT NULL PRIMARY KEY,
    Status VARCHAR(20) NOT NULL,
    Amount DECIMAL(10,2) NOT NULL
);

INSERT INTO IsolationDemo (Id, Status, Amount) VALUES
    (1, 'Pending', 100.00),
    (2, 'Pending', 200.00),
    (3, 'Pending', 300.00);

SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
SELECT Id, Status, Amount FROM IsolationDemo WHERE Amount BETWEEN 0 AND 500;

INSERT INTO IsolationDemo (Id, Status, Amount) VALUES (4, 'Pending', 400.00);

SELECT Id, Status, Amount FROM IsolationDemo WHERE Amount BETWEEN 0 AND 500;
COMMIT;

DELETE FROM IsolationDemo WHERE Id = 4;

SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRAN;
SELECT Id, Status, Amount FROM IsolationDemo WHERE Amount BETWEEN 0 AND 500;

INSERT INTO IsolationDemo (Id, Status, Amount) VALUES (4, 'Pending', 400.00);

SELECT Id, Status, Amount FROM IsolationDemo WHERE Amount BETWEEN 0 AND 500;
COMMIT;

DROP TABLE IsolationDemo;
