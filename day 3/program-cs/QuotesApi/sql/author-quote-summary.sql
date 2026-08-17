WITH RankedQuotes AS (
    SELECT
        Author,
        Text,
        PublishedAt,
        ROW_NUMBER() OVER (PARTITION BY Author ORDER BY PublishedAt DESC) AS rn,
        COUNT(*) OVER (PARTITION BY Author) AS QuoteCount
    FROM Quotes
    WHERE IsDeleted = 0
)
SELECT
    Author,
    QuoteCount,
    Text AS MostRecentQuote,
    PublishedAt AS MostRecentPublishedAt
FROM RankedQuotes
WHERE rn = 1
ORDER BY QuoteCount DESC, Author;

WITH AuthorStats AS (
    SELECT
        Author,
        COUNT(*) AS QuoteCount,
        MAX(PublishedAt) AS LatestPublishedAt
    FROM Quotes
    WHERE IsDeleted = 0
    GROUP BY Author
)
SELECT
    a.Author,
    a.QuoteCount,
    q.Text AS MostRecentQuote,
    a.LatestPublishedAt
FROM AuthorStats a
INNER JOIN Quotes q
    ON q.Author = a.Author
    AND q.PublishedAt = a.LatestPublishedAt
ORDER BY a.QuoteCount DESC, a.Author;

WITH RECURSIVE DateRange(day) AS (
    SELECT date('2026-08-15')
    UNION ALL
    SELECT date(day, '+1 day')
    FROM DateRange
    WHERE day < date('2026-08-17')
),
DailyCounts AS (
    SELECT date(PublishedAt) AS day, COUNT(*) AS QuoteCount
    FROM Quotes
    WHERE IsDeleted = 0
    GROUP BY date(PublishedAt)
)
SELECT
    DateRange.day,
    COALESCE(DailyCounts.QuoteCount, 0) AS QuoteCount
FROM DateRange
LEFT JOIN DailyCounts ON DailyCounts.day = DateRange.day
ORDER BY DateRange.day;
