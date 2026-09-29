using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Contracts.Reports;
using Synapse.Blocks.Api.Data;

namespace Synapse.Blocks.Api.Reports;

public sealed class CaseReportService(AppDbContext db)
{
    public async Task<CaseReportDto?> GetAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var exists = await db.TeacherCases.AnyAsync(item => item.Id == caseId && item.OwnerId == ownerId, cancellationToken);
        if (!exists) return null;

        var participants = await db.Participants.AsNoTracking()
            .Where(participant => participant.CaseId == caseId && participant.Case.OwnerId == ownerId)
            .Include(participant => participant.Attempts).ThenInclude(attempt => attempt.LevelResults)
            .OrderBy(participant => participant.CreatedAt)
            .ToListAsync(cancellationToken);
        var rows = participants.GroupBy(participant => participant.NormalizedName)
            .Select(group =>
            {
                var attempts = group.SelectMany(participant => participant.Attempts)
                    .OrderBy(attempt => attempt.StartedAt)
                    .Select(attempt => new CaseAttemptReportDto(
                        attempt.Id,
                        attempt.Status,
                        attempt.StartedAt,
                        attempt.CompletedAt,
                        attempt.LevelResults.Count(result => result.Completed)))
                    .ToArray();
                return new CaseParticipantReportDto(group.First().DisplayName, attempts.Length == 0 ? 0 : attempts.Max(attempt => attempt.CompletedLevelCount), attempts);
            })
            .OrderBy(participant => participant.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new CaseReportDto(caseId, rows);
    }
}
