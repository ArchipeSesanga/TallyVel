using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain;
using Microsoft.EntityFrameworkCore;
using Tests.TestSupport;
using static Tests.TestSupport.StokvelFixtures;

namespace Tests.Integration;

/// <summary>Proof of payment: upload, replace, delete, download and who may do which.</summary>
[Collection("Postgres collection")]
public class ProofTests(PostgresApiFactory factory)
{
    // A real 1x1 PNG, a JPEG header with padding, and a tiny PDF.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==");
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9];
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n");

    private static byte[] Padded(byte[] header, int totalBytes)
    {
        var bytes = new byte[totalBytes];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    private record Setup(Guid StokvelId, TestUser Chair, TestUser Payer, Guid ContributionId);

    /// <summary>A stokvel with a chairperson, a member who paid in an open cycle, and that contribution.</summary>
    private async Task<Setup> NewContributionAsync()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var payer = await AddMemberAsync(factory, stokvelId, chair);
        (await TestApi.Client(factory, chair.AccessToken).PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", new { label = "2026-11" }))
            .EnsureSuccessStatusCode();
        var recorded = await TestApi.Client(factory, payer.AccessToken).SendAsync(
            WithKey(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions",
                content: JsonContent.Create(new { memberUserId = payer.Id, cycle = "2026-11", amountCents = 50000 })));
        recorded.EnsureSuccessStatusCode();
        var id = (await recorded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return new Setup(stokvelId, chair, payer, id);
    }

    private static MultipartFormDataContent FileForm(byte[] bytes, string fileName, string clientContentType, string field = "file")
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(clientContentType);
        return new MultipartFormDataContent { { part, field, fileName } };
    }

    private Task<HttpResponseMessage> Upload(Setup s, TestUser who, byte[] bytes, string fileName = "receipt.png", string clientType = "image/png", string field = "file") =>
        TestApi.Client(factory, who.AccessToken).PostAsync(ProofUrl(s), FileForm(bytes, fileName, clientType, field));

    private Task<HttpResponseMessage> Download(Setup s, TestUser who) => TestApi.Client(factory, who.AccessToken).GetAsync(ProofUrl(s));

    private static string ProofUrl(Setup s) => $"/api/v1/stokvels/{s.StokvelId}/contributions/{s.ContributionId}/proof";

    private async Task CloseTheCycleAsync(Guid stokvelId)
    {
        await using var db = factory.CreateContext();
        await db.ContributionCycles.Where(c => c.StokvelId == stokvelId)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.ClosedAt, DateTimeOffset.UtcNow));
    }

    private static async Task<JsonElement> Body(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    // ---- uploading ----

    [Fact]
    public async Task The_member_who_paid_attaches_a_PNG_and_can_download_exactly_those_bytes_with_the_safe_headers()
    {
        var s = await NewContributionAsync();

        var uploaded = await Upload(s, s.Payer, Png, "my receipt.png");
        var downloaded = await Download(s, s.Payer);

        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var meta = await Body(uploaded);
        Assert.Equal("image/png", meta.GetProperty("contentType").GetString());
        Assert.Equal("my receipt.png", meta.GetProperty("fileName").GetString());
        Assert.Equal(Png.Length, meta.GetProperty("sizeBytes").GetInt64());
        Assert.Equal(64, meta.GetProperty("sha256").GetString()!.Length);

        Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
        Assert.Equal(Png, await downloaded.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", downloaded.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", downloaded.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("my receipt.png", downloaded.Content.Headers.ContentDisposition?.FileNameStar ?? downloaded.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal("nosniff", downloaded.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("no-store", downloaded.Headers.CacheControl?.ToString());
        Assert.Contains("private", downloaded.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task A_treasurer_can_attach_and_download_a_proof_for_a_members_contribution()
    {
        var s = await NewContributionAsync();
        var treasurer = await AddMemberAsync(factory, s.StokvelId, s.Chair, MemberRole.Treasurer);

        var uploaded = await Upload(s, treasurer, Pdf, "bank.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Download(s, treasurer)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Download(s, s.Payer)).StatusCode);
    }

    [Fact]
    public async Task An_ordinary_member_cannot_attach_a_proof_to_someone_elses_contribution_and_gets_403()
    {
        var s = await NewContributionAsync();
        var other = await AddMemberAsync(factory, s.StokvelId, s.Chair);

        var response = await Upload(s, other, Png);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("proof-forbidden", (await Body(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_non_member_of_the_stokvel_gets_404_on_upload()
    {
        var s = await NewContributionAsync();
        var stranger = await TestApi.RegisterAndLoginAsync(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await Upload(s, stranger, Png)).StatusCode);
    }

    [Fact]
    public async Task A_text_file_renamed_to_pdf_gets_415_because_the_type_comes_from_the_bytes()
    {
        var s = await NewContributionAsync();

        var response = await Upload(s, s.Payer, Encoding.UTF8.GetBytes("just some words, not a PDF"), "receipt.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await Body(response);
        Assert.Equal("unsupported-file-type", body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task A_JPEG_is_accepted_whatever_name_and_Content_Type_the_client_claims_and_is_stored_as_image_jpeg()
    {
        var s = await NewContributionAsync();

        var response = await Upload(s, s.Payer, Jpeg, "photo.txt", "text/plain");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var meta = await Body(response);
        Assert.Equal("image/jpeg", meta.GetProperty("contentType").GetString());
        Assert.Equal("photo.jpg", meta.GetProperty("fileName").GetString()); // the extension follows the real type
        Assert.Equal("image/jpeg", (await Download(s, s.Payer)).Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_file_name_is_cleaned_of_paths_and_odd_characters()
    {
        var s = await NewContributionAsync();

        var response = await Upload(s, s.Payer, Png, "..\\..\\etc/pass<wd>x;y.png");

        var name = (await Body(response)).GetProperty("fileName").GetString()!;
        Assert.DoesNotContain("/", name);
        Assert.DoesNotContain("\\", name);
        Assert.DoesNotContain("<", name);
        Assert.DoesNotContain(";", name);
        Assert.EndsWith(".png", name);
    }

    [Fact]
    public async Task A_file_of_exactly_5_MB_is_accepted_and_one_byte_more_gets_413()
    {
        var s = await NewContributionAsync();
        const int limit = 5 * 1024 * 1024;

        var exactly = await Upload(s, s.Payer, Padded(Png, limit));
        var tooBig = await Upload(s, s.Payer, Padded(Png, limit + 1));

        Assert.Equal(HttpStatusCode.Created, exactly.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);
        Assert.Equal("application/problem+json", tooBig.Content.Headers.ContentType?.MediaType);
        Assert.Equal("file-too-large", (await Body(tooBig)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_far_bigger_upload_also_gets_a_clean_413_and_not_a_server_error()
    {
        var s = await NewContributionAsync();

        var response = await Upload(s, s.Payer, Padded(Png, 12 * 1024 * 1024));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_request_with_no_file_field_gets_400()
    {
        var s = await NewContributionAsync();

        var response = await Upload(s, s.Payer, Png, field: "attachment");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Uploading_again_replaces_the_proof_while_the_cycle_is_open()
    {
        var s = await NewContributionAsync();
        await Upload(s, s.Payer, Png);

        var replaced = await Upload(s, s.Payer, Pdf, "new.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var downloaded = await Download(s, s.Payer);
        Assert.Equal(Pdf, await downloaded.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", downloaded.Content.Headers.ContentType?.MediaType);
        await using var db = factory.CreateContext();
        Assert.Equal(1, await db.ContributionProofs.CountAsync(p => p.ContributionId == s.ContributionId)); // still one proof
    }

    // ---- deleting ----

    [Fact]
    public async Task Deleting_removes_the_proof_and_a_second_delete_gets_404()
    {
        var s = await NewContributionAsync();
        await Upload(s, s.Payer, Png);
        var client = TestApi.Client(factory, s.Payer.AccessToken);

        var first = await client.DeleteAsync(ProofUrl(s));
        var second = await client.DeleteAsync(ProofUrl(s));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Download(s, s.Payer)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task An_ordinary_member_cannot_delete_someone_elses_proof_and_gets_403()
    {
        var s = await NewContributionAsync();
        await Upload(s, s.Payer, Png);
        var other = await AddMemberAsync(factory, s.StokvelId, s.Chair);

        var response = await TestApi.Client(factory, other.AccessToken).DeleteAsync(ProofUrl(s));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Download(s, s.Payer)).StatusCode); // still there
    }

    // ---- locked ----

    [Fact]
    public async Task Once_the_cycle_is_closed_upload_replace_and_delete_all_get_409_proof_locked()
    {
        var s = await NewContributionAsync();
        await Upload(s, s.Payer, Png);
        await CloseTheCycleAsync(s.StokvelId);

        var replace = await Upload(s, s.Payer, Pdf, "new.pdf", "application/pdf");
        var delete = await TestApi.Client(factory, s.Payer.AccessToken).DeleteAsync(ProofUrl(s));

        Assert.Equal(HttpStatusCode.Conflict, replace.StatusCode);
        Assert.Equal("proof-locked", (await Body(replace)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal("proof-locked", (await Body(delete)).GetProperty("code").GetString());
        Assert.Equal(Png, await (await Download(s, s.Payer)).Content.ReadAsByteArrayAsync()); // unchanged
    }

    [Fact]
    public async Task A_first_upload_after_the_cycle_closed_is_locked_too()
    {
        var s = await NewContributionAsync();
        await CloseTheCycleAsync(s.StokvelId);

        var response = await Upload(s, s.Payer, Png);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- who can download ----

    [Fact]
    public async Task An_ordinary_member_and_another_stokvels_treasurer_get_the_same_404_as_a_contribution_with_no_proof()
    {
        var s = await NewContributionAsync();
        await Upload(s, s.Payer, Png);
        var ordinaryMember = await AddMemberAsync(factory, s.StokvelId, s.Chair);
        var (_, otherChair) = await NewStokvelAsync(factory); // runs a DIFFERENT stokvel
        var noProof = await NewContributionAsync();

        var asMember = await Download(s, ordinaryMember);
        var asOtherTreasurer = await Download(s, otherChair);
        var whenNoProofExists = await Download(noProof, noProof.Payer);

        Assert.Equal(HttpStatusCode.NotFound, asMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asOtherTreasurer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, whenNoProofExists.StatusCode);
        // The same answer: same status, same code, same message, so nothing says "a proof exists but you can't see it".
        var reference = await Body(whenNoProofExists);
        foreach (var refused in new[] { asMember, asOtherTreasurer })
        {
            var body = await Body(refused);
            Assert.Equal(reference.GetProperty("code").GetString(), body.GetProperty("code").GetString());
            Assert.Equal(reference.GetProperty("detail").GetString(), body.GetProperty("detail").GetString());
            Assert.Equal(reference.GetProperty("title").GetString(), body.GetProperty("title").GetString());
        }
    }

    [Fact]
    public async Task A_chairperson_of_this_stokvel_can_download_it_like_a_treasurer()
    {
        var s = await NewContributionAsync();
        await Upload(s, s.Payer, Png);

        Assert.Equal(HttpStatusCode.OK, (await Download(s, s.Chair)).StatusCode);
    }

    // ---- the flags on a contribution ----

    [Fact]
    public async Task Contributions_say_whether_they_have_a_proof_and_whether_the_caller_may_download_it()
    {
        var s = await NewContributionAsync();
        await Upload(s, s.Payer, Png);
        var ordinaryMember = await AddMemberAsync(factory, s.StokvelId, s.Chair);

        async Task<(bool Has, bool Can)> FlagsAs(TestUser who)
        {
            var list = await TestApi.Client(factory, who.AccessToken).GetFromJsonAsync<JsonElement>($"/api/v1/stokvels/{s.StokvelId}/contributions");
            var item = list.GetProperty("items").EnumerateArray().Single();
            var single = await TestApi.Client(factory, who.AccessToken).GetFromJsonAsync<JsonElement>($"/api/v1/stokvels/{s.StokvelId}/contributions/{s.ContributionId}");
            Assert.Equal(item.GetProperty("canDownloadProof").GetBoolean(), single.GetProperty("canDownloadProof").GetBoolean()); // list and single agree
            return (item.GetProperty("hasProof").GetBoolean(), item.GetProperty("canDownloadProof").GetBoolean());
        }

        Assert.Equal((true, true), await FlagsAs(s.Payer));          // the uploader
        Assert.Equal((true, true), await FlagsAs(s.Chair));          // this stokvel's chairperson
        Assert.Equal((true, false), await FlagsAs(ordinaryMember));  // exists, but not theirs to download
    }

    [Fact]
    public async Task A_contribution_without_a_proof_says_so_to_everyone()
    {
        var s = await NewContributionAsync();

        var list = await TestApi.Client(factory, s.Payer.AccessToken).GetFromJsonAsync<JsonElement>($"/api/v1/stokvels/{s.StokvelId}/contributions");
        var item = list.GetProperty("items").EnumerateArray().Single();

        Assert.False(item.GetProperty("hasProof").GetBoolean());
        Assert.False(item.GetProperty("canDownloadProof").GetBoolean());
    }
}
