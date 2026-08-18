SELECT OrderId, CustomerId, Status, Amount FROM Orders WHERE CustomerId = 2500;

DROP INDEX IX_Orders_CustomerId ON Orders;
CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_Covering
    ON Orders (CustomerId)
    INCLUDE (Status, Amount);

SELECT OrderId, CustomerId, Status, Amount FROM Orders WHERE CustomerId = 2500;
