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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).HasMaxLength(320);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.FullName).HasMaxLength(User.MaxFullNameLength);
        });

        modelBuilder.Entity<Stokvel>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(Stokvel.MaxNameLength);
            e.Property(s => s.ContributionAmount).HasPrecision(18, 2);
            e.Property(s => s.Cycle).HasConversion<string>().HasMaxLength(20);

            e.HasMany(s => s.Members)
                .WithOne()
                .HasForeignKey(m => m.StokvelId)
                .OnDelete(DeleteBehavior.Cascade);

            e.Navigation(s => s.Members)
                .HasField("_members")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<StokvelMember>(e =>
        {
            e.HasKey(m => new { m.StokvelId, m.UserId });
            e.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContributionCycle>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Label).HasMaxLength(ContributionCycle.MaxLabelLength);
            e.HasAlternateKey(c => new { c.StokvelId, c.Label });
            // Lets Payout reference (StokvelId, Id) so a payout's stokvel
            // must match its cycle's stokvel.
            e.HasAlternateKey(c => new { c.StokvelId, c.Id });
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne<Stokvel>()
                .WithMany()
                .HasForeignKey(c => c.StokvelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Contribution>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Amount).HasPrecision(18, 2);
            e.Property(c => c.Cycle).HasMaxLength(ContributionCycle.MaxLabelLength);
            e.HasIndex(c => new { c.StokvelId, c.MemberUserId, c.Cycle }).IsUnique();

            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.MemberUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<ContributionCycle>()
                .WithMany()
                .HasForeignKey(c => new { c.StokvelId, c.Cycle })
                .HasPrincipalKey(cc => new { cc.StokvelId, cc.Label })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payout>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.HasIndex(p => p.ContributionCycleId).IsUnique();

            e.HasOne<ContributionCycle>()
                .WithMany()
                .HasForeignKey(p => new { p.StokvelId, p.ContributionCycleId })
                .HasPrincipalKey(c => new { c.StokvelId, c.Id })
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<StokvelMember>()
                .WithMany()
                .HasForeignKey(p => new { p.StokvelId, p.RecipientUserId })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
