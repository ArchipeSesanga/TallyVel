using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

/// <summary>
/// An abstraction over where Stokvels are stored. Same rationale as
/// <see cref="IUserRepository"/> — this is the seam that lets a real
/// database replace the in-memory store later without touching
/// controllers.
/// </summary>
public interface IStokvelRepository
{
    IReadOnlyCollection<Stokvel> GetAll();
    Stokvel? GetById(Guid id);
    void Add(Stokvel stokvel);
}