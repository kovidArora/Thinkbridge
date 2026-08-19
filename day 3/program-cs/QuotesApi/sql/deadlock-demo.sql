CREATE TABLE Resources (
    ResourceId INT PRIMARY KEY,
    Value INT NOT NULL
);

INSERT INTO Resources (ResourceId, Value) VALUES (1, 100), (2, 200);

-- ============================================================
-- DEADLOCKS: reversed lock order between sessions
-- ============================================================

-- SESSION A
BEGIN TRAN;
UPDATE Resources SET Value = 111 WHERE ResourceId = 1;

-- SESSION B
BEGIN TRAN;
UPDATE Resources SET Value = 333 WHERE ResourceId = 2;

-- SESSION A (blocks on Resource 2, held by B)
UPDATE Resources SET Value = 222 WHERE ResourceId = 2;

-- SESSION B (blocks on Resource 1, held by A -> circular wait -> one session is killed as deadlock victim)
UPDATE Resources SET Value = 444 WHERE ResourceId = 1;

-- whichever session survives:
COMMIT TRAN;

UPDATE Resources SET Value = 100 WHERE ResourceId = 1;
UPDATE Resources SET Value = 200 WHERE ResourceId = 2;

-- ============================================================
-- FIXED: consistent lock order (both sessions touch Resource 1 first)
-- ============================================================

-- SESSION A
BEGIN TRAN;
UPDATE Resources SET Value = 111 WHERE ResourceId = 1;

-- SESSION B (blocks on Resource 1, held by A -- no deadlock, just waits)
BEGIN TRAN;
UPDATE Resources SET Value = 444 WHERE ResourceId = 1;

-- SESSION A
UPDATE Resources SET Value = 222 WHERE ResourceId = 2;
COMMIT TRAN;

-- SESSION B (unblocks once Session A commits)
UPDATE Resources SET Value = 333 WHERE ResourceId = 2;
COMMIT TRAN;

DROP TABLE Resources;
