SELECT c."Id", c."Amount", c."Cycle", c."MemberUserId", c."RecordedAt", c."StokvelId"
FROM "Contributions" AS c
WHERE c."StokvelId" = '30d8a9e8-727e-41d2-b828-5d2daf5cb2c1' AND c."Cycle" = '2026-09'
ORDER BY c."RecordedAt", c."Id"
LIMIT 21
