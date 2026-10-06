SELECT c."Id", c."Amount", c."Cycle", c."MemberUserId", c."RecordedAt", c."StokvelId"
FROM "Contributions" AS c
WHERE c."StokvelId" = 'a76414e6-754c-4655-b5db-93d39e0a1c25' AND c."Cycle" = '2025-06' AND (c."RecordedAt", c."Id") > ('2025-06-14T13:36:03.0000000+00:00'::timestamptz, 'f9496263-6575-468c-bc0f-72f6b36d9906'::uuid)
ORDER BY c."RecordedAt", c."Id"
LIMIT 21
