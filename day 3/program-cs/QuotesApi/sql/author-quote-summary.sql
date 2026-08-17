-- Author summary: quote count + most-recent quote per author, in one statement.
-- Uses a non-recursive CTE with window functions instead of a correlated subquery:
-- a correlated subquery re-executes per outer row (O(n^2)-shaped), while the CTE
-- computes rank + count in a single pass over Quotes, then filters.

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

-- Alternative: aggregate CTE + INNER JOIN back to the base table.
-- Same result, but can return duplicate rows if two quotes from the same
-- author share the exact same PublishedAt timestamp (a tie the JOIN can't
-- disambiguate, unlike ROW_NUMBER() which always picks exactly one row).

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

-- Recursive CTE: quotes-per-day report that doesn't silently drop zero-count
-- days. DateRange recursively generates every day in the window; the LEFT JOIN
-- (not INNER JOIN) is what keeps days with no quotes in the result at all.

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
