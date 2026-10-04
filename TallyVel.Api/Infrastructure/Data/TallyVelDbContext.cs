using Microsoft.EntityFrameworkCore;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

public class TallyVelDbContext : DbContext
{
    public TallyVelDbContext(DbContextOptions<TallyVelDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Stokvel> Stokvels => Set<Stokvel>();
    public DbSet<StokvelMember> StokvelMembers => Set<StokvelMember>();
    public DbSet<ContributionCycle> ContributionCycles => Set<ContributionCycle>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<Payout> Payouts => Set<Payout>();

    // Each entity's table, keys, constraints and relationships live in its
    // own IEntityTypeConfiguration class in this folder; this picks them all up.
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TallyVelDbContext).Assembly);
}
