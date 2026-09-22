using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

/// <summary>
/// An abstraction over where Contributions are stored. Same rationale
/// as <see cref="IStokvelRepository"/>.
/// </summary>
public interface IContributionRepository
{
    IReadOnlyCollection<Contribution> GetAll();
    Contribution? GetById(Guid id);
    void Add(Contribution contribution);
}
