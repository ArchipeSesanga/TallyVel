using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Microsoft.EntityFrameworkCore;
using Tests.TestSupport;
using static Tests.TestSupport.StokvelFixtures;

namespace Tests.Integration;

/// <summary>The close preview and the safe, repeatable close of a cycle.</summary>
[Collection("Postgres collection")]
public class CycleCloseTests(PostgresApiFactory factory)
{
    private record Setup(Guid StokvelId, Guid CycleId, TestUser Chair, TestUser Payer, TestUser Debtor);

    /// <summary>
    /// A stokvel (R500 each) with a chairperson, a member who pays and a member who does not, and one open cycle.
    /// The chairperson pays in full; the debtor pays nothing, so only the debtor is in arrears.
    /// </summary>
    private async Task<Setup> NewOpenCycleAsync(bool debtorPaysNothing = true)
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var payer = await AddMemberAsync(factory, stokvelId, chair);
        var debtor = await AddMemberAsync(factory, stokvelId, chair);
        var client = TestApi.Client(factory, chair.AccessToken);
        var cycle = await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", new { label = "2026-11" });
        cycle.EnsureSuccessStatusCode();
        var cycleId = (await cycle.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await RecordAsync(stokvelId, chair, chair.Id, 50000);
        await RecordAsync(stokvelId, payer, payer.Id, 50000);
        if (!debtorPaysNothing) await RecordAsync(stokvelId, debtor, debtor.Id, 50000);
        return new Setup(stokvelId, cycleId, chair, payer, debtor);
    }

    private async Task RecordAsync(Guid stokvelId, TestUser by, Guid forUser, long cents)
    {
        var response = await TestApi.Client(factory, by.AccessToken).SendAsync(WithKey(HttpMethod.Post,
            $"/api/v1/stokvels/{stokvelId}/contributions",
            content: JsonContent.Create(new { memberUserId = forUser, cycle = "2026-11", amountCents = cents })));
        response.EnsureSuccessStatusCode();
    }

    private Task<HttpResponseMessage> Preview(Setup s, TestUser who) =>
        TestApi.Client(factory, who.AccessToken).GetAsync($"/api/v1/stokvels/{s.StokvelId}/cycles/{s.CycleId}/close-preview");

    private Task<HttpResponseMessage> Close(Setup s, TestUser who, string version, string? key = null) =>
        TestApi.Client(factory, who.AccessToken).SendAsync(WithKey(HttpMethod.Post,
            $"/api/v1/stokvels/{s.StokvelId}/cycles/{s.CycleId}/close", key,
            JsonContent.Create(new { version })));

    private static async Task<JsonElement> Body(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<int> ArrearRowsAsync(Guid cycleId)
    {
        await using var db = factory.CreateContext();
        return await db.Arrears.CountAsync(a => a.CycleId == cycleId);
    }

    private async Task<bool> IsClosedAsync(Guid cycleId)
    {
        await using var db = factory.CreateContext();
        return (await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == cycleId)).ClosedAt is not null;
    }

    // ---- the preview ----

    [Fact]
    public async Task The_preview_lists_who_would_be_in_arrears_with_its_cycle_date_total_and_version()
    {
        var s = await NewOpenCycleAsync();

        var response = await Preview(s, s.Chair);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        Assert.Equal(s.CycleId, body.GetProperty("cycleId").GetGuid());
        Assert.Equal("2026-11", body.GetProperty("cycleLabel").GetString());
        Assert.Equal(StokvelCalendar.Today(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd"), body.GetProperty("asOfDate").GetString());
        var arrears = body.GetProperty("arrears").EnumerateArray().Single();
        Assert.Equal(s.Debtor.Id, arrears.GetProperty("memberUserId").GetGuid());
        Assert.Equal("Leak Test", arrears.GetProperty("fullName").GetString());
        Assert.Equal(50000, arrears.GetProperty("shortfallCents").GetInt64());
        Assert.Equal(50000, body.GetProperty("totalArrearsCents").GetInt64());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("version").GetString()));
    }

    [Fact]
    public async Task Previewing_twice_with_nothing_changed_gives_the_same_version()
    {
        var s = await NewOpenCycleAsync();

        var first = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString();
        var second = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task The_version_changes_when_a_contribution_is_recorded_and_when_a_member_joins()
    {
        var s = await NewOpenCycleAsync();
        var before = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString();

        await RecordAsync(s.StokvelId, s.Chair, s.Debtor.Id, 20000); // a partial payment, recorded by the chairperson
        var afterPayment = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString();
        await AddMemberAsync(factory, s.StokvelId, s.Chair);
        var afterJoin = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString();

        Assert.NotEqual(before, afterPayment);
        Assert.NotEqual(afterPayment, afterJoin);
    }

    [Fact]
    public async Task An_ordinary_member_gets_403_on_the_preview_and_a_non_member_gets_404()
    {
        var s = await NewOpenCycleAsync();
        var stranger = await TestApi.RegisterAndLoginAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await Preview(s, s.Payer)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Preview(s, stranger)).StatusCode);
    }

    [Fact]
    public async Task A_cycle_that_belongs_to_another_stokvel_is_404()
    {
        var s = await NewOpenCycleAsync();
        var (otherStokvel, otherChair) = await NewStokvelAsync(factory);

        var response = await TestApi.Client(factory, otherChair.AccessToken)
            .GetAsync($"/api/v1/stokvels/{otherStokvel}/cycles/{s.CycleId}/close-preview");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- the close ----

    [Fact]
    public async Task The_close_returns_exactly_what_the_preview_showed_and_records_the_arrears_once()
    {
        var s = await NewOpenCycleAsync();
        var preview = await Body(await Preview(s, s.Chair));

        var response = await Close(s, s.Chair, preview.GetProperty("version").GetString()!);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var closed = await Body(response);
        Assert.Equal(s.CycleId, closed.GetProperty("cycleId").GetGuid());
        Assert.EndsWith("Z", closed.GetProperty("closedAt").GetString());
        Assert.Equal(preview.GetProperty("totalArrearsCents").GetInt64(), closed.GetProperty("totalArrearsCents").GetInt64());
        Assert.Equal(preview.GetProperty("arrears").ToString(), closed.GetProperty("arrears").ToString()); // the very same list
        Assert.Equal(1, await ArrearRowsAsync(s.CycleId));
        Assert.True(await IsClosedAsync(s.CycleId));
    }

    [Fact]
    public async Task Closing_opens_the_next_cycle_like_the_nightly_job_does()
    {
        var s = await NewOpenCycleAsync();
        var version = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString()!;

        (await Close(s, s.Chair, version)).EnsureSuccessStatusCode();

        await using var db = factory.CreateContext();
        Assert.Equal(2, await db.ContributionCycles.CountAsync(c => c.StokvelId == s.StokvelId));
    }

    [Fact]
    public async Task A_contribution_recorded_between_the_preview_and_the_close_gets_409_stale_and_nothing_is_closed()
    {
        var s = await NewOpenCycleAsync();
        var version = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString()!;
        await RecordAsync(s.StokvelId, s.Chair, s.Debtor.Id, 50000); // the debtor pays, after the treasurer looked

        var response = await Close(s, s.Chair, version);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("close-preview-stale", (await Body(response)).GetProperty("code").GetString());
        Assert.Equal(0, await ArrearRowsAsync(s.CycleId));
        Assert.False(await IsClosedAsync(s.CycleId));
        // A fresh preview now shows nobody owing, and closing with ITS version works.
        var fresh = await Body(await Preview(s, s.Chair));
        Assert.Empty(fresh.GetProperty("arrears").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await Close(s, s.Chair, fresh.GetProperty("version").GetString()!)).StatusCode);
    }

    [Fact]
    public async Task A_refused_stale_close_does_not_use_up_its_Idempotency_Key()
    {
        var s = await NewOpenCycleAsync();
        var stale = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString()!;
        await RecordAsync(s.StokvelId, s.Chair, s.Debtor.Id, 50000);
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Conflict, (await Close(s, s.Chair, stale, key)).StatusCode);
        var fresh = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString()!;

        var retry = await Close(s, s.Chair, fresh, key); // the same key, with the new version

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task Repeating_a_close_with_the_same_key_returns_the_original_200_and_records_the_arrears_once()
    {
        var s = await NewOpenCycleAsync();
        var version = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString()!;
        var key = Guid.NewGuid().ToString();

        var first = await Close(s, s.Chair, version, key);
        var again = await Close(s, s.Chair, version, key);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal((await Body(first)).ToString(), (await Body(again)).ToString()); // same closedAt, same list
        Assert.Equal(1, await ArrearRowsAsync(s.CycleId));
    }

    [Fact]
    public async Task Two_closes_at_the_same_time_with_the_same_key_close_once_and_both_get_the_same_answer()
    {
        var s = await NewOpenCycleAsync();
        var version = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString()!;
        var key = Guid.NewGuid().ToString();

        var both = await Task.WhenAll(Close(s, s.Chair, version, key), Close(s, s.Chair, version, key));

        Assert.All(both, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal((await Body(both[0])).ToString(), (await Body(both[1])).ToString());
        Assert.Equal(1, await ArrearRowsAsync(s.CycleId));
    }

    [Fact]
    public async Task Two_closes_with_different_keys_and_the_same_version_close_once_and_the_other_gets_409_already_closed()
    {
        var s = await NewOpenCycleAsync();
        var second = await AddMemberAsync(factory, s.StokvelId, s.Chair, MemberRole.Treasurer);
        var preview = await Body(await Preview(s, s.Chair));
        var version = preview.GetProperty("version").GetString()!;

        var both = await Task.WhenAll(Close(s, s.Chair, version), Close(s, second, version));

        Assert.Equal(1, both.Count(r => r.StatusCode == HttpStatusCode.OK));
        var loser = Assert.Single(both, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("cycle-already-closed", (await Body(loser)).GetProperty("code").GetString());
        // Both members who did not pay (the debtor and the new treasurer) are recorded once: not twice, as two closes would.
        Assert.Equal(preview.GetProperty("arrears").GetArrayLength(), await ArrearRowsAsync(s.CycleId));
        Assert.Equal(2, await ArrearRowsAsync(s.CycleId));
    }

    [Fact]
    public async Task A_cycle_already_closed_by_the_nightly_job_gets_409_on_the_preview_and_on_a_close()
    {
        var s = await NewOpenCycleAsync();
        var version = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString()!;
        await using (var db = factory.CreateContext())
            await db.ContributionCycles.Where(c => c.Id == s.CycleId)
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.ClosedAt, DateTimeOffset.UtcNow));

        var preview = await Preview(s, s.Chair);
        var close = await Close(s, s.Chair, version);

        Assert.Equal(HttpStatusCode.Conflict, preview.StatusCode);
        Assert.Equal("cycle-already-closed", (await Body(preview)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);
        Assert.Equal("cycle-already-closed", (await Body(close)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Nobody_in_arrears_gives_an_empty_list_and_the_close_still_works()
    {
        var s = await NewOpenCycleAsync(debtorPaysNothing: false);

        var preview = await Body(await Preview(s, s.Chair));
        Assert.Empty(preview.GetProperty("arrears").EnumerateArray());
        Assert.Equal(0, preview.GetProperty("totalArrearsCents").GetInt64());

        var response = await Close(s, s.Chair, preview.GetProperty("version").GetString()!);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await Body(response)).GetProperty("arrears").EnumerateArray());
        Assert.Equal(0, await ArrearRowsAsync(s.CycleId));
        Assert.True(await IsClosedAsync(s.CycleId));
    }

    [Fact]
    public async Task An_ordinary_member_gets_403_on_a_close_and_a_non_member_404_and_nothing_is_closed()
    {
        var s = await NewOpenCycleAsync();
        var version = (await Body(await Preview(s, s.Chair))).GetProperty("version").GetString()!;
        var stranger = await TestApi.RegisterAndLoginAsync(factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await Close(s, s.Payer, version)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Close(s, stranger, version)).StatusCode);
        Assert.False(await IsClosedAsync(s.CycleId));
    }

    [Fact]
    public async Task A_close_without_an_Idempotency_Key_or_without_a_version_gets_400()
    {
        var s = await NewOpenCycleAsync();
        var client = TestApi.Client(factory, s.Chair.AccessToken);
        var url = $"/api/v1/stokvels/{s.StokvelId}/cycles/{s.CycleId}/close";

        var noKey = await client.PostAsJsonAsync(url, new { version = "x" });
        var noVersion = await Close(s, s.Chair, "");

        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
        Assert.False(await IsClosedAsync(s.CycleId));
    }

    [Fact]
    public async Task The_nightly_job_records_exactly_the_arrears_the_preview_showed()
    {
        // Both call CycleClosing. Move a fake clock past the cycle's last day so the JOB closes it, then compare.
        var s = await NewOpenCycleAsync();
        var preview = await Body(await Preview(s, s.Chair));
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        clock.SetUtcNow(DateTimeOffset.UtcNow.AddMonths(2)); // well after the cycle ended

        await factory.CreateCloser(clock).RunOnceAsync(CancellationToken.None);

        await using var db = factory.CreateContext();
        var rows = await db.Arrears.AsNoTracking().Where(a => a.CycleId == s.CycleId).ToListAsync();
        var fromJob = rows.OrderBy(r => r.MemberUserId).Select(r => (r.MemberUserId, r.ShortfallCents)).ToList();
        var fromPreview = preview.GetProperty("arrears").EnumerateArray()
            .Select(a => (a.GetProperty("memberUserId").GetGuid(), a.GetProperty("shortfallCents").GetInt64()))
            .OrderBy(a => a.Item1).ToList();
        Assert.Equal(fromPreview, fromJob);
        Assert.NotEmpty(fromJob);
    }
}
