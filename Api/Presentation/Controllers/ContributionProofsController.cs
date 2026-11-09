using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Api.Application.Contracts;
using Api.Application.Services;
using Api.Authorization;
using Api.Common;
using Domain;

namespace Api.Presentation.Controllers;

/// <summary>
/// Refuses a body that DECLARES (Content-Length) more than the limit, before the form is read, as the usual
/// 413 problem+json. A body that lies about its length, or has none (chunked), is stopped by the endpoint's
/// RequestSizeLimit and Kestrel's own limit while it is read; the error handler turns that into the same 413.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RejectOversizedBodyAttribute(int maxBytes) : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (context.HttpContext.Request.ContentLength > maxBytes)
            throw new PayloadTooLargeException("file-too-large",
                $"The file is larger than {ContributionProof.MaxBytes / (1024 * 1024)} MB.");
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}

[ApiController]
[Route(ApiRoutes.V1 + "/stokvels/{stokvelId:guid}/contributions/{contributionId:guid}/proof")]
public class ContributionProofsController(ProofService proofs) : StokvelControllerBase
{
    // 5 MB of file plus room for the multipart wrapping (boundaries, headers). Used by the endpoint attributes
    // AND Kestrel's own limit in Program.cs, so a 5 MB file gets in and a bigger one is stopped with a clean 413.
    public const int MaxRequestBytes = ContributionProof.MaxBytes + 64 * 1024;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RejectOversizedBody(MaxRequestBytes)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [EnableRateLimiting(RateLimitPolicies.Money)]
    [EndpointSummary("Attach or replace the proof of payment")]
    [EndpointDescription(
        "multipart/form-data with one file field named \"file\". The contribution's own member, or a Treasurer or Chairperson " +
        "of this stokvel. JPEG, PNG or PDF, decided by the file's first bytes (never its name or Content-Type), at most 5 MB. " +
        "Uploading again replaces the proof while the cycle is open. Once the cycle is closed or paid out it is locked (409).")]
    [ProducesResponseType<ProofResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProofResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<IActionResult> Upload(Guid stokvelId, Guid contributionId, IFormFile? file, CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Member) is { } denied) return denied;

        if (file is null)
            throw new ArgumentException("Send the file in a multipart/form-data field named \"file\".");
        if (file.Length > ContributionProof.MaxBytes)
            throw new PayloadTooLargeException("file-too-large",
                $"The file is larger than {ContributionProof.MaxBytes / (1024 * 1024)} MB.");

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(buffer, ct);

        var (proof, replaced) = await proofs.SaveAsync(stokvelId, contributionId, CallerId, file.FileName, buffer.ToArray(), ct);
        return StatusCode(replaced ? StatusCodes.Status200OK : StatusCodes.Status201Created, proof);
    }

    [HttpDelete]
    [EndpointSummary("Remove the proof of payment")]
    [EndpointDescription("Same people as upload, and only while the cycle is open (otherwise 409 proof-locked).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid stokvelId, Guid contributionId, CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Member) is { } denied) return denied;

        await proofs.DeleteAsync(stokvelId, contributionId, CallerId, ct);
        return NoContent();
    }

    [HttpGet]
    [EndpointSummary("Download the proof of payment")]
    [EndpointDescription(
        "Only the contribution's member and this stokvel's Treasurer or Chairperson. Everyone else, including an ordinary " +
        "member of the same stokvel and a treasurer of another stokvel, gets the same 404 as a contribution with no proof. " +
        "The file is sent as an attachment with the detected Content-Type, X-Content-Type-Options: nosniff and " +
        "Cache-Control: private, no-store.")]
    [Produces("image/jpeg", "image/png", "application/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Download(Guid stokvelId, Guid contributionId, CancellationToken ct)
    {
        // No membership check first: "not a member" and "no proof here" must look exactly the same.
        var proof = await proofs.GetForDownloadAsync(stokvelId, contributionId, CallerId, ct);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        // Giving File() a download name makes the response "Content-Disposition: attachment".
        return File(proof.Content, proof.ContentType, proof.FileName);
    }
}
