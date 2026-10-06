SELECT c."Id", c."Amount", c."Cycle", c."MemberUserId", c."RecordedAt", c."StokvelId"
FROM "Contributions" AS c
WHERE c."StokvelId" = 'a76414e6-754c-4655-b5db-93d39e0a1c25' AND c."Cycle" = '2025-06'
ORDER BY c."RecordedAt", c."Id"
LIMIT 21
