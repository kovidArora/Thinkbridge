-- Starts from the same Orders table + IX_Orders_CustomerId index as indexing-demo.sql.

-- BEFORE: this query needs Status and Amount, which the plain CustomerId index
-- doesn't store, so every matching row needs a separate lookup back into the
-- clustered index. 62 logical reads. Plan shows two operators:
--   Index Seek(IX_Orders_CustomerId) -> Clustered Index Seek(...) LOOKUP
SELECT OrderId, CustomerId, Status, Amount FROM Orders WHERE CustomerId = 2500;

-- Replace the index with a covering one: store Status/Amount right in the
-- non-clustered index's leaf pages via INCLUDE, so nothing needs looking up.
DROP INDEX IX_Orders_CustomerId ON Orders;
CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_Covering
    ON Orders (CustomerId)
    INCLUDE (Status, Amount);

-- AFTER: same query, 2 logical reads. Plan shows a single Index Seek on
-- IX_Orders_CustomerId_Covering -- the lookup operator is gone entirely.
SELECT OrderId, CustomerId, Status, Amount FROM Orders WHERE CustomerId = 2500;
