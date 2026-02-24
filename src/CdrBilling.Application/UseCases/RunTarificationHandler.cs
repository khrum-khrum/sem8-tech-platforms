using CdrBilling.Application.Abstractions;
using CdrBilling.Domain.Enums;
using CdrBilling.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CdrBilling.Application.UseCases;

public sealed record RunTarificationCommand(Guid SessionId) : IRequest;

public sealed class RunTarificationHandler(
    ICallRecordRepository cdrRepo,
    ITariffRepository tariffRepo,
    IBillingSessionRepository sessionRepo,
    ISessionProgressReporter progress,
    ILogger<RunTarificationHandler> logger)
    : IRequestHandler<RunTarificationCommand>
{
    private const int BatchSize = 500;

    public async Task Handle(RunTarificationCommand request, CancellationToken cancellationToken)
    {
        var sessionId = request.SessionId;

        try
        {
            // Count total CDR records for progress tracking
            var total = await cdrRepo.CountAsync(sessionId, cancellationToken);

            var session = await sessionRepo.GetAsync(sessionId, cancellationToken)
                ?? throw new InvalidOperationException($"Session {sessionId} not found.");

            session.SetRunning(total);
            await sessionRepo.UpdateAsync(session, cancellationToken);

            // Load all tariffs into memory and build the prefix trie engine
            var tariffs = await tariffRepo.GetAllForSessionAsync(sessionId, cancellationToken);
            var engine = new TarificationEngine(tariffs);

            logger.LogInformation(
                "Tariffication started for session {SessionId}: {Total} records, {TariffCount} tariffs.",
                sessionId, total, tariffs.Count);

            var processed = 0;
            var updates = new List<(long Id, decimal Charge, long TariffId)>(BatchSize);

            await foreach (var call in cdrRepo.GetUnratedAsync(sessionId, cancellationToken))
            {
                var match = engine.FindBestTariff(call);

                if (match is not null)
                    updates.Add((call.Id, match.Charge, match.Tariff.Id));
                // Records with no tariff match keep ComputedCharge = null (unrated)

                processed++;

                if (updates.Count >= BatchSize)
                {
                    await cdrRepo.BulkUpdateChargesAsync(updates, cancellationToken);
                    updates.Clear();
                    await progress.ReportAsync(sessionId, processed, total, cancellationToken);

                    logger.LogDebug("Tariffication progress: {Processed}/{Total}", processed, total);
                }
            }

            // Final flush
            if (updates.Count > 0)
            {
                await cdrRepo.BulkUpdateChargesAsync(updates, cancellationToken);
                updates.Clear();
            }

            await progress.ReportAsync(sessionId, processed, total, cancellationToken);

            session = await sessionRepo.GetAsync(sessionId, cancellationToken)
                ?? throw new InvalidOperationException($"Session {sessionId} not found after tariffication.");
            session.MarkCompleted();
            await sessionRepo.UpdateAsync(session, cancellationToken);

            await progress.ReportCompletedAsync(sessionId, cancellationToken);

            logger.LogInformation(
                "Tariffication completed for session {SessionId}: {Processed} records processed.",
                sessionId, processed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Tariffication failed for session {SessionId}.", sessionId);

            try
            {
                var session = await sessionRepo.GetAsync(sessionId, CancellationToken.None);
                if (session is not null)
                {
                    session.MarkFailed(ex.Message);
                    await sessionRepo.UpdateAsync(session, CancellationToken.None);
                }
                await progress.ReportFailedAsync(sessionId, ex.Message, CancellationToken.None);
            }
            catch (Exception inner)
            {
                logger.LogError(inner, "Failed to persist error state for session {SessionId}.", sessionId);
            }
        }
    }
}
