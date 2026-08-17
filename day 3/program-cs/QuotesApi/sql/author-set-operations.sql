-- No Tags/Category table exists in the real schema. Collections/CollectionItem
-- stand in as tag categories here (two collections named 'classic' and 'modern').

SELECT Author FROM Quotes
EXCEPT
SELECT q.Author FROM Quotes q JOIN CollectionItem ci ON ci.QuoteId = q.Id;

SELECT q.Author FROM Quotes q
JOIN CollectionItem ci ON ci.QuoteId = q.Id
JOIN Collections c ON c.Id = ci.CollectionId
WHERE c.Name = 'classic'
INTERSECT
SELECT q.Author FROM Quotes q
JOIN CollectionItem ci ON ci.QuoteId = q.Id
JOIN Collections c ON c.Id = ci.CollectionId
WHERE c.Name = 'modern';

SELECT q.Author FROM Quotes q
JOIN CollectionItem ci ON ci.QuoteId = q.Id
JOIN Collections c ON c.Id = ci.CollectionId
WHERE c.Name = 'classic'
UNION
SELECT q.Author FROM Quotes q
JOIN CollectionItem ci ON ci.QuoteId = q.Id
JOIN Collections c ON c.Id = ci.CollectionId
WHERE c.Name = 'modern';
