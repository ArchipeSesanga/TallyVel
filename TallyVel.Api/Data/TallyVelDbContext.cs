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
    public DbSet<Contribution> Contributions => Set<Contribution>();
}
