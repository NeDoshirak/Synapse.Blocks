using Synapse.Blocks.Api.Cases;

namespace Synapse.Blocks.Api.Operations;

public sealed class CaseArchiveService(CaseService cases)
{
    public Task<bool> ArchiveAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
        => cases.ArchiveAsync(ownerId, caseId, cancellationToken);

    public Task<bool> RestoreAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
        => cases.RestoreAsync(ownerId, caseId, cancellationToken);
}
