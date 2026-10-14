using System.Diagnostics;
using Api.Observability;
using Domain;

namespace Api.Application.Services;

/// <summary>Owns the money path: the payout span, the payout metric and the payout log line.</summary>
public sealed class PayoutService(
    ICycleRepository cycles, TallyVelMetrics metrics, ILogger<PayoutService> logger)
{
    public async Task<Payout> ProcessAsync(
        Guid stokvelId, Guid cycleId, Guid recipientUserId, CancellationToken ct)
    {
        // Child of the request span; the repository's SQL spans become ITS children.
        using var activity = TallyVelTelemetry.Source.StartActivity("payout.process");
        activity?.SetTag("tallyvel.cycle.id", cycleId);

        try
        {
            var payout = await cycles.PayOutAsync(stokvelId, cycleId, recipientUserId, ct);
            Finish(activity, PayoutOutcome.Succeeded);
            logger.LogInformation("Payout {PayoutId} processed for cycle {CycleId}", payout.Id, cycleId);
            return payout;
        }
        catch (TallyVelException ex)
        {
            // An expected refusal (already paid out, nothing to pay...).
            Finish(activity, PayoutOutcome.From(ex), errorType: ex.Code);
            throw;   // TallyVelExceptionHandler still turns it into 404/422
        }
        catch (Exception ex)
        {
            // A real fault: database down, a bug.
            Finish(activity, PayoutOutcome.Error, errorType: ex.GetType().Name);
            throw;
        }
    }

    private void Finish(Activity? activity, string outcome, string? errorType = null)
    {
        metrics.PayoutProcessed(outcome);
        activity?.SetTag("tallyvel.payout.outcome", outcome);

        if (outcome == PayoutOutcome.Succeeded)
            return;

        // The description is our own fixed outcome string, never ex.Message.
        activity?.SetTag("error.type", errorType);
        activity?.SetStatus(ActivityStatusCode.Error, outcome);
    }
}
