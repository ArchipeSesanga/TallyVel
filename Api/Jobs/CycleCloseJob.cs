using Microsoft.Extensions.Options;

namespace Api.Jobs;

/// <summary>Runs the cycle closer once at startup, then every interval.</summary>
public sealed class CycleCloseJob(
    CycleCloser closer,
    IOptions<CycleCloseOptions> options,
    TimeProvider clock,
    ILogger<CycleCloseJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
            return;

        await Task.Yield();   // don't hold up host startup
        using var timer = new PeriodicTimer(opts.Interval, clock);
        try
        {
            do
            {
                try
                {
                    await closer.RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Keeps a database outage from stopping the host.
                    logger.LogError(ex, "Cycle-close run failed; retrying in {Interval}", opts.Interval);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}
