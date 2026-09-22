using TallyVel.Api.Common;
using TallyVel.Api.Data;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Services;

public class ContributionService
{
    private readonly IUserRepository _userRepository;
    private readonly IStokvelRepository _stokvelRepository;
    private readonly IContributionRepository _contributionRepository;

    public ContributionService(
        IUserRepository userRepository,
        IStokvelRepository stokvelRepository,
        IContributionRepository contributionRepository)
    {
        _userRepository = userRepository;
        _stokvelRepository = stokvelRepository;
        _contributionRepository = contributionRepository;
    }

    public Contribution AddContribution(Guid stokvelId, Guid contributorId, decimal amount)
    {
        _ = _stokvelRepository.GetById(stokvelId)
            ?? throw new NotFoundException($"No stokvel found with id {stokvelId}.");

        _ = _userRepository.GetById(contributorId)
            ?? throw new NotFoundException($"No user found with id {contributorId}.");

        var contribution = new Contribution(stokvelId, contributorId, amount);
        _contributionRepository.Add(contribution);

        return contribution;
    }
}
