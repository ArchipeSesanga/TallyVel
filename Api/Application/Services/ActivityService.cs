using System.Buffers.Text;
using System.Text.Json;
using Api.Application.Contracts;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Services;

/// <summary>
/// The member's activity feed: contributions recorded, payouts made and members joined, across ONLY the
/// stokvels the caller belongs to, newest first. The union, ordering and paging all run in the database.
/// </summary>
public sealed class ActivityService(TallyVelDbContext db)
{
    // One shape for all three kinds, so they can be joined into one list (UNION ALL) and ordered together.
    private sealed class Row
    {
        public ActivityKind Kind { get; init; }
        // Unique across the three kinds ("c:<id>", "p:<id>", "m:<stokvelId>:<userId>"). It breaks ties between
        // rows with the same time, so the order is always the same and a page never repeats or skips a row.
        public string Key { get; init; } = "";
        public Guid StokvelId { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
        public decimal? Amount { get; init; }
        public Guid PersonUserId { get; init; }
    }

    // What a page token holds: where the previous page ended.
    private sealed record Token(DateTimeOffset LastOccurredAt, string LastKey)
    {
        public string Encode() => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));

        public static Token? Decode(string text)
        {
            try { return JsonSerializer.Deserialize<Token>(Base64Url.DecodeFromChars(text)); }
            catch { return null; }
        }
    }

    public async Task<PagedResponse<ActivityItem>> GetAsync(Guid userId, int pageSize, string? pageToken, CancellationToken ct)
    {
        // The stokvels the caller belongs to. Every source below is limited to them: nothing else can appear.
        var mine = db.StokvelMembers.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.StokvelId);

        var contributions = db.Contributions.AsNoTracking()
            .Where(c => mine.Contains(c.StokvelId))
            .Select(c => new Row
            {
                Kind = ActivityKind.ContributionRecorded, Key = "c:" + c.Id.ToString(), StokvelId = c.StokvelId,
                OccurredAt = c.RecordedAt, Amount = c.Amount, PersonUserId = c.MemberUserId,
            });

        var payouts = db.Payouts.AsNoTracking()
            .Where(p => mine.Contains(p.StokvelId))
            .Select(p => new Row
            {
                Kind = ActivityKind.PayoutMade, Key = "p:" + p.Id.ToString(), StokvelId = p.StokvelId,
                OccurredAt = p.PaidAt, Amount = p.Amount, PersonUserId = p.RecipientUserId,
            });

        var joins = db.StokvelMembers.AsNoTracking()
            .Where(m => mine.Contains(m.StokvelId))
            .Select(m => new Row
            {
                Kind = ActivityKind.MemberJoined, Key = "m:" + m.StokvelId.ToString() + ":" + m.UserId.ToString(),
                StokvelId = m.StokvelId, OccurredAt = m.JoinedAt, Amount = null, PersonUserId = m.UserId,
            });

        var feed = contributions.Concat(payouts).Concat(joins);

        // Keyset paging: continue strictly after the last row of the previous page (newest first).
        if (!string.IsNullOrEmpty(pageToken))
        {
            var t = Token.Decode(pageToken)
                ?? throw new InvalidQueryException("pageToken", "Invalid page token.");
            feed = feed.Where(r => EF.Functions.LessThan(
                ValueTuple.Create(r.OccurredAt, r.Key),
                ValueTuple.Create(t.LastOccurredAt, t.LastKey)));
        }

        var rows = await feed
            .OrderByDescending(r => r.OccurredAt).ThenByDescending(r => r.Key)
            .Take(pageSize + 1) // one extra row tells us whether there is another page
            .ToListAsync(ct);

        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        // Names, for this page only. A person's name is shown only while they are still a member of that
        // stokvel, which is the rule that already governs who may see whom.
        var stokvelIds = rows.Select(r => r.StokvelId).Distinct().ToList();
        var stokvelNames = await db.Stokvels.AsNoTracking()
            .Where(s => stokvelIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var memberNames = (await db.StokvelMembers.AsNoTracking()
                .Where(m => stokvelIds.Contains(m.StokvelId))
                .Select(m => new { m.StokvelId, m.UserId, m.User.FullName })
                .ToListAsync(ct))
            .ToDictionary(m => (m.StokvelId, m.UserId), m => m.FullName);

        var items = rows.Select(r => new ActivityItem(
            r.Key,
            r.Kind,
            r.StokvelId,
            stokvelNames[r.StokvelId],
            r.OccurredAt,
            r.Amount is { } amount ? Money.ToCents(amount) : null,
            memberNames.GetValueOrDefault((r.StokvelId, r.PersonUserId)))).ToList();

        // "" on the last page, exactly like the contributions list.
        var next = hasMore ? new Token(rows[^1].OccurredAt, rows[^1].Key).Encode() : string.Empty;
        return new PagedResponse<ActivityItem>(items, next);
    }
}
