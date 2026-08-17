SELECT
    Author,
    PublishedAt,
    Text,
    ROW_NUMBER() OVER (PARTITION BY Author ORDER BY PublishedAt) AS QuoteNumber,
    LAG(PublishedAt) OVER (PARTITION BY Author ORDER BY PublishedAt) AS PreviousQuoteAt,
    CAST(
        julianday(PublishedAt) - julianday(LAG(PublishedAt) OVER (PARTITION BY Author ORDER BY PublishedAt))
        AS INTEGER
    ) AS DaysSincePrevious
FROM Quotes
WHERE IsDeleted = 0
ORDER BY Author, PublishedAt;
