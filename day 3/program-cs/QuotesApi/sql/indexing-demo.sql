-- Run against SQL Server (SQLite has no SET STATISTICS IO / execution plan viewer).
-- Table generated with ~100k rows via a numbers-CTE cross join.

CREATE TABLE Orders (
    OrderId INT IDENTITY(1,1) NOT NULL,
    CustomerId INT NOT NULL,
    OrderDate DATETIME2 NOT NULL,
    Status VARCHAR(20) NOT NULL,
    Amount DECIMAL(10,2) NOT NULL,
    Notes VARCHAR(200) NOT NULL,
    CONSTRAINT PK_Orders PRIMARY KEY NONCLUSTERED (OrderId)
);

;WITH Numbers AS (
    SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT INTO Orders (CustomerId, OrderDate, Status, Amount, Notes)
SELECT
    (n % 5000) + 1,
    DATEADD(MINUTE, n, '2024-01-01'),
    CASE (n % 4) WHEN 0 THEN 'Pending' WHEN 1 THEN 'Shipped' WHEN 2 THEN 'Delivered' ELSE 'Cancelled' END,
    CAST((n % 500) + 1 AS DECIMAL(10,2)),
    'Order note ' + CAST(n AS VARCHAR(10))
FROM Numbers;

-- Index DDL

ALTER TABLE Orders DROP CONSTRAINT PK_Orders;
ALTER TABLE Orders ADD CONSTRAINT PK_Orders_Clustered PRIMARY KEY CLUSTERED (OrderId);

CREATE NONCLUSTERED INDEX IX_Orders_CustomerId ON Orders (CustomerId);
CREATE NONCLUSTERED INDEX IX_Orders_OrderDate ON Orders (OrderDate);

-- Q1: uses the clustered index (range scan by OrderId)
SET STATISTICS IO ON;
SELECT * FROM Orders WHERE OrderId BETWEEN 50000 AND 50500;

-- Q2: uses IX_Orders_CustomerId (point lookup)
SELECT * FROM Orders WHERE CustomerId = 2500;

-- Q3: uses IX_Orders_OrderDate (narrow, selective range)
SELECT * FROM Orders WHERE OrderDate BETWEEN '2024-03-01 00:00:00' AND '2024-03-01 01:00:00';
